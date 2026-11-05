"""Speaker diarization via pyannote.audio (requires HF token + accepted model license)."""

from __future__ import annotations

_pipeline_cache: dict[tuple, object] = {}

MODEL_NAME = "pyannote/speaker-diarization-3.1"


class DiarizationUnavailable(RuntimeError):
    pass


def _patch_hf_hub_compat() -> None:
    """pyannote 3.x passes use_auth_token= into hf_hub_download, removed in hub 1.x."""
    import functools

    import huggingface_hub

    def wrap(fn):
        if getattr(fn, "_use_auth_token_compat", False):
            return fn

        @functools.wraps(fn)
        def patched(*args, use_auth_token=None, token=None, **kwargs):
            return fn(*args, token=token or use_auth_token, **kwargs)

        patched._use_auth_token_compat = True
        return patched

    for name in ("hf_hub_download", "snapshot_download"):
        fn = getattr(huggingface_hub, name, None)
        if fn is not None:
            setattr(huggingface_hub, name, wrap(fn))


def _load_pipeline(hf_token: str | None, device: str):
    _patch_hf_hub_compat()
    try:
        from pyannote.audio import Pipeline
    except ImportError as e:
        raise DiarizationUnavailable(
            "pyannote.audio is not installed. Run: pip install pyannote.audio"
        ) from e

    try:
        try:
            pipeline = Pipeline.from_pretrained(MODEL_NAME, token=hf_token)
        except TypeError:
            pipeline = Pipeline.from_pretrained(MODEL_NAME, use_auth_token=hf_token)
    except Exception as e:
        raise DiarizationUnavailable(
            f"Cannot load {MODEL_NAME}. Provide a valid HF_TOKEN and accept the model "
            f"license at https://huggingface.co/{MODEL_NAME}\nOriginal error: {e}"
        ) from e

    if device == "cuda":
        try:
            import torch
            pipeline.to(torch.device("cuda"))
        except Exception:
            pass
    return pipeline


def diarize(
    wav_path: str,
    hf_token: str | None = None,
    device: str = "auto",
    num_speakers: int | None = None,
) -> list[tuple[float, float, str]]:
    """Return sorted [(start, end, speaker)] turns."""
    from core import resolve_device

    device, _ = resolve_device(device, None)
    key = (hf_token, device)
    if key not in _pipeline_cache:
        _pipeline_cache[key] = _load_pipeline(hf_token, device)
    pipeline = _pipeline_cache[key]

    kwargs = {"num_speakers": num_speakers} if num_speakers else {}
    result = pipeline(wav_path, **kwargs)

    turns = [
        (turn.start, turn.end, speaker)
        for turn, _, speaker in result.itertracks(yield_label=True)
    ]
    return sorted(turns)


_inference_cache: dict = {}


def _get_inference(hf_token: str | None):
    """Sliding-window speaker embedding model (wespeaker via pyannote Inference)."""
    if "inf" not in _inference_cache:
        _patch_hf_hub_compat()
        from pyannote.audio import Inference

        last_err = None
        for name in ("pyannote/embedding", "pyannote/wespeaker-voxceleb-resnet34-LM"):
            try:
                _inference_cache["inf"] = Inference(name, use_auth_token=hf_token)
                break
            except Exception as e:
                last_err = e
        else:
            raise DiarizationUnavailable(f"Cannot load speaker embedding model: {last_err}")
    return _inference_cache["inf"]


def extract_voiceprints(
    wav_path: str,
    turns: list[tuple[float, float, str]],
    hf_token: str | None = None,
) -> dict[str, list[float]] | None:
    """Per-speaker voiceprint: sliding-window embeddings assigned to the
    max-overlap speaker turn, duration-weighted mean, L2-normalized."""
    import numpy as np

    if not turns:
        return None
    inf = _get_inference(hf_token)
    embs = inf({"audio": wav_path})  # {Segment: np.ndarray}

    acc: dict[str, tuple["np.ndarray", float]] = {}
    for seg, emb in embs.items():
        best_spk, best_ov = None, 0.0
        for s, e, spk in turns:
            ov = min(seg.end, e) - max(seg.start, s)
            if ov > best_ov:
                best_ov, best_spk = ov, spk
        if best_spk is None or best_ov <= 0:
            continue
        prev = acc.get(best_spk)
        acc[best_spk] = (
            (prev[0] + emb * best_ov, prev[1] + best_ov)
            if prev is not None
            else (emb * best_ov, best_ov)
        )

    out: dict[str, list[float]] = {}
    for spk, (vec, weight) in acc.items():
        v = vec / max(weight, 1e-9)
        norm = float(np.linalg.norm(v))
        if norm > 0:
            out[spk] = (v / norm).astype(float).tolist()
    return out or None
