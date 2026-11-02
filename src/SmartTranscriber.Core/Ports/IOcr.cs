namespace SmartTranscriber.Core.Ports;

/// <summary>Runs OCR on a frame image (tesseract or similar).</summary>
public interface IOcr
{
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task<string> OcrAsync(string imagePath, CancellationToken ct = default);
}
