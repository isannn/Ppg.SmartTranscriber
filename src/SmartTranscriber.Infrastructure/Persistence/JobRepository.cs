using Dapper;

using Npgsql;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Persistence;

public sealed class JobRepository(NpgsqlDataSource dataSource) : IJobRepository
{
    private const string JobColumns =
        "id, object_name AS ObjectName, original_file_name AS OriginalFileName, status, " +
        "created_at AS CreatedAt, completed_at AS CompletedAt, error_message AS ErrorMessage, " +
        "model, language, duration_seconds AS DurationSeconds";

    public async Task<TranscriptionJob?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var job = await conn.QuerySingleOrDefaultAsync<TranscriptionJob>(
            $"SELECT {JobColumns} FROM transcription_jobs WHERE id = @id", new { id });
        if (job is null)
        {
            return null;
        }

        var segments = await conn.QueryAsync<TranscriptSegment>(
            "SELECT id, job_id AS JobId, start_time AS StartTime, end_time AS EndTime, speaker, text " +
            "FROM transcript_segments WHERE job_id = @id ORDER BY start_time", new { id });
        job.Segments = segments.ToList();
        job.SegmentCount = job.Segments.Count;
        return job;
    }

    public async Task<IReadOnlyList<TranscriptionJob>> ListAsync(JobStatus? status, int limit, int offset, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var sql = $"SELECT {JobColumns}, " +
                  "(SELECT count(*) FROM transcript_segments s WHERE s.job_id = j.id) AS segment_count " +
                  "FROM transcription_jobs j " +
                  (status is null ? "" : "WHERE status = @status ") +
                  "ORDER BY created_at DESC LIMIT @limit OFFSET @offset";

        var rows = await conn.QueryAsync<JobRow>(sql, new { status = status?.ToString(), limit, offset });
        return rows.Select(r => r.ToJob()).ToList();
    }

    public async Task<TranscriptionJob?> ClaimNextPendingAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<TranscriptionJob>(
            $"UPDATE transcription_jobs SET status = 'Processing' " +
            "WHERE id = (SELECT id FROM transcription_jobs WHERE status = 'Pending' " +
            "ORDER BY created_at FOR UPDATE SKIP LOCKED LIMIT 1) " +
            $"RETURNING {JobColumns}");
    }

    public async Task AddAsync(TranscriptionJob job, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            "INSERT INTO transcription_jobs " +
            "(id, object_name, original_file_name, status, created_at, completed_at, error_message, model, language, duration_seconds) " +
            "VALUES (@Id, @ObjectName, @OriginalFileName, @Status, @CreatedAt, @CompletedAt, @ErrorMessage, @Model, @Language, @DurationSeconds)",
            job);
    }

    public async Task UpdateAsync(TranscriptionJob job, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE transcription_jobs SET status = @Status, completed_at = @CompletedAt, " +
            "error_message = @ErrorMessage, model = @Model, language = @Language, " +
            "duration_seconds = @DurationSeconds WHERE id = @Id", job);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync("DELETE FROM transcription_jobs WHERE id = @id", new { id });
    }

    private sealed class JobRow
    {
        public Guid Id { get; set; }
        public string ObjectName { get; set; } = "";
        public string OriginalFileName { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Model { get; set; }
        public string? Language { get; set; }
        public double? DurationSeconds { get; set; }
        public int SegmentCount { get; set; }

        public TranscriptionJob ToJob()
        {
            return new TranscriptionJob
            {
                Id = Id,
                ObjectName = ObjectName,
                OriginalFileName = OriginalFileName,
                Status = Enum.Parse<JobStatus>(Status, ignoreCase: true),
                CreatedAt = CreatedAt,
                CompletedAt = CompletedAt,
                ErrorMessage = ErrorMessage,
                Model = Model,
                Language = Language,
                DurationSeconds = DurationSeconds,
                SegmentCount = SegmentCount
            };
        }
    }
}
