using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;

namespace SmartTranscriber.Core.Ports;

public interface IJobRepository
{
    Task<TranscriptionJob?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TranscriptionJob>> ListAsync(JobStatus? status, string? tag, int limit, int offset, CancellationToken ct = default);
    Task<TranscriptionJob?> ClaimNextPendingAsync(CancellationToken ct = default);
    Task AddAsync(TranscriptionJob job, CancellationToken ct = default);
    Task UpdateAsync(TranscriptionJob job, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<JobStatsDto> GetStatsAsync(CancellationToken ct = default);
}
