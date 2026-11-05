"""Core pipeline: media -> wav (ffmpeg) -> faster-whisper -> speaker merge -> txt."""

from __future__ import annotations

import math
import os
import subprocess
import tempfile
from dataclasses import dataclass

MEDIA_EXTENSIONS = {
    ".mp4", ".mkv", ".webm", ".mp3", ".m4a", ".wav",
    ".ogg", ".flac", ".avi", ".mov", ".aac", ".wma", ".opus",
}
DEFAULT_MODEL = "gigaam-v3-e2e-rnnt"

# onnx-asr model names. e2e variants output punctuated text and keep English
# terms in Latin script; raw v3 ctc/rnnt have a Cyrillic-only 33-char vocab.
GIGAAM_MODELS = {
    "gigaam-v3-e2e-rnnt",
    "gigaam-v3-e2e-ctc",
    "gigaam-v3-ctc",
    "gigaam-v3-rnnt",
    "gigaam-multilingual-ctc",
    "gigaam-multilingual-large-ctc",
}
# gigaam-package style aliases
GIGAAM_ALIASES = {
    "v3_ctc": "gigaam-v3-ctc",
    "v3_rnnt": "gigaam-v3-rnnt",
    "v3_e2e_ctc": "gigaam-v3-e2e-ctc",
    "v3_e2e_rnnt": "gigaam-v3-e2e-rnnt",
    "multilingual_ctc": "gigaam-multilingual-ctc",
    "multilingual_large_ctc": "gigaam-multilingual-large-ctc",
}

_model_cache: dict[tuple, object] = {}
_cuda_dll_cookie = None


def _ensure_cuda_dlls() -> None:
    """Let ctranslate2 find cublas/cudnn bundled inside torch's lib dir.

    Also preloads torch's cudnn64_9.dll so a by-name lookup from ctranslate2
    (which ships its own cudnn stub) resolves to this same consistent set —
    mixing stub/siblings of different cudnn versions hard-crashes the process.
    """
    global _cuda_dll_cookie
    if _cuda_dll_cookie is not None or not hasattr(os, "add_dll_directory"):
        return
    try:
        import torch
    except ImportError:
        return
    lib = os.path.join(os.path.dirname(torch.__file__), "lib")
    if not os.path.isdir(lib):
        return
    _cuda_dll_cookie = os.add_dll_directory(lib)
    import ctypes
    for name in ("cudnn64_9.dll", "cublas64_12.dll", "cublasLt64_12.dll"):
        try:
            ctypes.CDLL(os.path.join(lib, name))
        except OSError:
            pass


@dataclass
class Word:
    start: float
    end: float
    text: str
    speaker: str | None = None
    confidence: float | None = None


@dataclass
class Block:
    speaker: str | None
    start: float
    end: float
    text: str
    confidence: float | None = None


def resolve_device(device: str, compute_type: str | None) -> tuple[str, str]:
    # must run before `import ctranslate2` (statically loads its own cudnn64_9.dll)
    _ensure_cuda_dlls()
    if device == "auto":
        try:
            import ctranslate2
            device = "cuda" if ctranslate2.get_cuda_device_count() > 0 else "cpu"
        except Exception:
            device = "cpu"
    if compute_type is None:
        compute_type = "float16" if device == "cuda" else "int8"
    return device, compute_type


def extract_audio(media_path: str, work_dir: str) -> str:
    """Extract mono 16 kHz wav via ffmpeg."""
    wav_path = os.path.join(work_dir, "audio_16k.wav")
    cmd = [
        "ffmpeg", "-y", "-i", media_path,
        "-vn", "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le",
        wav_path,
    ]
    proc = subprocess.run(cmd, capture_output=True, text=True)
    if proc.returncode != 0 or not os.path.exists(wav_path):
        raise RuntimeError(f"ffmpeg failed on {media_path}:\n{proc.stderr[-1500:]}")
    return wav_path


def get_model(model_size: str, device: str, compute_type: str):
    key = (model_size, device, compute_type)
    if key not in _model_cache:
        if device == "cuda":
            _ensure_cuda_dlls()
        from faster_whisper import WhisperModel
        _model_cache[key] = WhisperModel(model_size, device=device, compute_type=compute_type)
    return _model_cache[key]


def is_gigaam(model_size: str) -> bool:
    return model_size in GIGAAM_MODELS or model_size in GIGAAM_ALIASES or model_size.startswith("gigaam")


def _onnx_providers(device: str) -> list[str]:
    # must run before onnxruntime creates CUDA sessions: torch's lib dir holds
    # the cudart/cublas/cudnn DLLs the provider loads by name
    _ensure_cuda_dlls()
    import onnxruntime

    if device in ("auto", "cuda") and "CUDAExecutionProvider" in onnxruntime.get_available_providers():
        return ["CUDAExecutionProvider", "CPUExecutionProvider"]
    if device == "cuda":
        print("WARNING: CUDAExecutionProvider unavailable in onnxruntime, using CPU")
    return ["CPUExecutionProvider"]


def _get_gigaam_model(model_name: str, providers: list[str], quantization: str | None):
    key = ("onnx", model_name, tuple(providers), quantization)
    if key not in _model_cache:
        import onnx_asr

        vad = onnx_asr.load_vad("silero", providers=providers)
        # order matters: with_vad() on a timestamped adapter drops timestamps
        _model_cache[key] = (
            onnx_asr.load_model(model_name, providers=providers, quantization=quantization)
            .with_vad(vad, min_silence_duration_ms=500)
            .with_timestamps()
        )
    return _model_cache[key]


def _wav_duration(wav_path: str) -> float:
    try:
        import soundfile as sf

        return float(sf.info(wav_path).duration)
    except Exception:
        import wave

        with wave.open(wav_path, "rb") as w:
            return w.getnframes() / w.getframerate()


def _transcribe_gigaam(
    wav_path: str,
    model_name: str,
    language: str | None = None,
    device: str = "auto",
    compute_type: str | None = None,
    progress=None,
) -> tuple[list[Word], dict]:
    model_name = GIGAAM_ALIASES.get(model_name, model_name)
    multilingual = model_name.startswith("gigaam-multilingual")
    if language and language != "ru" and not multilingual:
        print(f"WARNING: {model_name} supports Russian only; ignoring language={language}")
        language = None

    providers = _onnx_providers(device)
    quantization = "int8" if compute_type == "int8" else None
    model = _get_gigaam_model(model_name, providers, quantization)
    duration = _wav_duration(wav_path)

    words: list[Word] = []
    for seg in model.recognize(wav_path, language=language):
        if seg.tokens and seg.timestamps and len(seg.tokens) == len(seg.timestamps):
            # timestamps are seconds relative to the segment start
            ends = seg.timestamps[1:] + [seg.end - seg.start]
            words.extend(
                Word(seg.start + t0, seg.start + max(t1, t0 + 0.01), tok)
                for tok, t0, t1 in zip(seg.tokens, seg.timestamps, ends)
                if tok
            )
        else:
            words.append(Word(seg.start, seg.end, seg.text))
        if progress and duration:
            progress(min(seg.end / duration, 0.99))

    meta = {
        "language": language or ("auto" if multilingual else "ru"),
        "language_probability": None,
        "duration": duration,
        "model": model_name,
        "device": "cuda" if providers[0] == "CUDAExecutionProvider" else "cpu",
        "compute_type": quantization or "fp32",
    }
    return words, meta


def transcribe_words(
    wav_path: str,
    model_size: str = DEFAULT_MODEL,
    language: str | None = None,
    device: str = "auto",
    compute_type: str | None = None,
    progress=None,
    hotwords: str | None = None,
) -> tuple[list[Word], dict]:
    """Transcribe wav, return word list + info. progress(frac: float) is optional."""
    if is_gigaam(model_size):
        return _transcribe_gigaam(wav_path, model_size, language, device, compute_type, progress)
    device, compute_type = resolve_device(device, compute_type)
    model = get_model(model_size, device, compute_type)

    segments, info = model.transcribe(
        wav_path,
        language=language,
        word_timestamps=True,
        vad_filter=True,
        vad_parameters={"min_silence_duration_ms": 500},
        condition_on_previous_text=False,
        hotwords=hotwords,
    )

    words: list[Word] = []
    duration = info.duration or 0
    for seg in segments:
        # avg_logprob is a natural log-probability; exp() approximates a 0..1 score
        seg_conf = None
        avg_lp = getattr(seg, "avg_logprob", None)
        if avg_lp is not None:
            seg_conf = min(max(math.exp(avg_lp), 0.0), 1.0)
        if seg.words:
            for w in seg.words:
                if w.start is not None and w.end is not None:
                    conf = getattr(w, "probability", None)
                    words.append(Word(w.start, w.end, w.word,
                                      confidence=conf if conf is not None else seg_conf))
        else:
            words.append(Word(seg.start, seg.end, seg.text, confidence=seg_conf))
        if progress and duration:
            progress(min(seg.end / duration, 0.99))

    meta = {
        "language": info.language,
        "language_probability": info.language_probability,
        "duration": info.duration,
        "model": model_size,
        "device": device,
        "compute_type": compute_type,
    }
    return words, meta


def assign_speakers(words: list[Word], turns: list[tuple[float, float, str]]) -> None:
    """Assign each word the speaker of the diarization turn with max overlap (in-place)."""
    if not turns:
        return
    turns = sorted(turns)
    ti = 0
    last_speaker = turns[0][2]
    for w in words:
        while ti < len(turns) - 1 and turns[ti][1] <= w.start:
            ti += 1
        best, best_ov = None, 0.0
        for j in range(ti, len(turns)):
            s, e, spk = turns[j]
            if s >= w.end:
                break
            ov = min(w.end, e) - max(w.start, s)
            if ov > best_ov:
                best_ov, best = ov, spk
        if best is not None:
            last_speaker = best
        w.speaker = best or last_speaker


def words_to_blocks(words: list[Word], merge_gap: float = 1.5) -> list[Block]:
    """Group consecutive words by speaker into blocks."""
    blocks: list[Block] = []
    confs: list[list[float]] = []
    for w in words:
        conf = w.confidence
        if blocks and w.speaker == blocks[-1].speaker and w.start - blocks[-1].end <= merge_gap:
            blocks[-1].text += w.text
            blocks[-1].end = w.end
            if conf is not None:
                confs[-1].append(conf)
        else:
            blocks.append(Block(w.speaker, w.start, w.end, w.text.lstrip()))
            confs.append([conf] if conf is not None else [])
    for b, cs in zip(blocks, confs):
        b.confidence = round(sum(cs) / len(cs), 3) if cs else None
    return blocks


def fmt_ts(t: float) -> str:
    t = max(0, int(t))
    return f"{t // 3600:02d}:{t % 3600 // 60:02d}:{t % 60:02d}"


def write_txt(txt_path: str, media_path: str, blocks: list[Block], meta: dict) -> None:
    prob = meta["language_probability"]
    prob_str = f"{prob:.0%}" if prob is not None else "n/a"
    with open(txt_path, "w", encoding="utf-8") as f:
        f.write(f"source: {media_path}\n")
        f.write(
            f"model: {meta['model']} ({meta['device']}/{meta['compute_type']}) | "
            f"language: {meta['language']} ({prob_str}) | "
            f"duration: {fmt_ts(meta['duration'])} | "
            f"diarization: {'on' if meta.get('diarization') else 'off'}\n"
        )
        f.write("=" * 70 + "\n\n")
        for b in blocks:
            label = f" {b.speaker}" if b.speaker else ""
            f.write(f"[{fmt_ts(b.start)} --> {fmt_ts(b.end)}]{label}\n{b.text.strip()}\n\n")


def process_media(
    media_path: str,
    out_dir: str,
    model_size: str = DEFAULT_MODEL,
    language: str | None = None,
    device: str = "auto",
    compute_type: str | None = None,
    diarize: bool = True,
    hf_token: str | None = None,
    num_speakers: int | None = None,
    hotwords: str | None = None,
    progress=None,
) -> str:
    """Full pipeline for one media file. Returns path to .txt."""
    os.makedirs(out_dir, exist_ok=True)
    base = os.path.splitext(os.path.basename(media_path))[0]
    txt_path = os.path.join(out_dir, base + ".txt")

    def report(frac, stage=""):
        if progress:
            progress(frac, stage)

    with tempfile.TemporaryDirectory() as tmp:
        report(0.0, "extracting audio")
        wav_path = extract_audio(media_path, tmp)

        report(0.02, "transcribing")
        words, meta = transcribe_words(
            wav_path, model_size, language, device, compute_type,
            progress=lambda f: report(0.02 + f * 0.75, "transcribing"),
            hotwords=hotwords,
        )

        turns = []
        if diarize:
            report(0.8, "diarizing")
            from diarization import diarize as run_diarization
            turns = run_diarization(wav_path, hf_token, device, num_speakers)
            meta["diarization"] = True

        if turns:
            assign_speakers(words, turns)
        blocks = words_to_blocks(words)
        if not blocks:
            report(0.97, "WARNING: no speech recognized")

        report(0.97, "writing txt")
        write_txt(txt_path, media_path, blocks, meta)

    report(1.0, "done")
    return txt_path
