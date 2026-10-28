using Dapper;

using Npgsql;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Persistence;

public sealed class SegmentRepository(NpgsqlDataSource dataSource) : ISegmentRepository
{
    private const string SegmentColumns =
        "id, job_id AS JobId, start_time AS StartTime, end_time AS EndTime, speaker, text";

    public async Task<IReadOnlyList<TranscriptSegment>> GetByJobAsync(Guid jobId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<TranscriptSegment>(
            $"SELECT {SegmentColumns} FROM transcript_segments WHERE job_id = @jobId ORDER BY start_time",
            new { jobId });
        return rows.ToList();
    }

    public async Task AddRangeAsync(IEnumerable<TranscriptSegment> segments, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            "INSERT INTO transcript_segments (job_id, start_time, end_time, speaker, text) " +
            "VALUES (@JobId, @StartTime, @EndTime, @Speaker, @Text)",
            segments);
    }

    public async Task<bool> UpdateSpeakerAsync(long segmentId, string? speaker, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(
            "UPDATE transcript_segments SET speaker = @speaker WHERE id = @segmentId",
            new { segmentId, speaker });
        return affected > 0;
    }

    public async Task<IReadOnlyList<SearchHitDto>> SearchAsync(string query, int limit, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<SearchHitDto>(
            "SELECT s.id AS SegmentId, s.job_id AS JobId, j.original_file_name AS OriginalFileName, " +
            "s.start_time AS StartTime, s.end_time AS EndTime, s.speaker AS Speaker, " +
            "ts_headline('russian', s.text, plainto_tsquery('russian', @query)) AS Snippet " +
            "FROM transcript_segments s " +
            "JOIN transcription_jobs j ON j.id = s.job_id " +
            "WHERE s.text_vector @@ plainto_tsquery('russian', @query) " +
            "ORDER BY ts_rank(s.text_vector, plainto_tsquery('russian', @query)) DESC " +
            "LIMIT @limit",
            new { query, limit });
        return rows.ToList();
    }
}
