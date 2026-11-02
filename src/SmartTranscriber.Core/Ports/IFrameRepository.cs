using SmartTranscriber.Core.Domain;

namespace SmartTranscriber.Core.Ports;

public interface IFrameRepository
{
    Task InsertBatchAsync(IReadOnlyList<MediaFrame> frames, CancellationToken ct = default);
    Task<IReadOnlyList<MediaFrame>> GetByJobAsync(Guid jobId, CancellationToken ct = default);
    Task<MediaFrame?> GetAsync(Guid jobId, int frameNo, CancellationToken ct = default);
    Task UpdateOcrAsync(Guid jobId, int frameNo, string ocrText, CancellationToken ct = default);
}
