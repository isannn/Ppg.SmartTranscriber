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
