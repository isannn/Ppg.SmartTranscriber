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
        if (affected == 1)
        {
            await InvalidateSummaryAsync(conn, segmentId);
        }
        return affected == 1;
    }

    public async Task<bool> UpdateTextAsync(long segmentId, string text, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        // text_vector is a generated column — it re-indexes itself
        var affected = await conn.ExecuteAsync(
            "UPDATE transcript_segments SET text = @text WHERE id = @segmentId",
            new { segmentId, text });
        if (affected == 1)
        {
            await InvalidateSummaryAsync(conn, segmentId);
        }
        return affected == 1;
    }

    /// <summary>A segment edit makes the job's cached summary stale — drop it.</summary>
    private static async Task InvalidateSummaryAsync(NpgsqlConnection conn, long segmentId)
    {
        await conn.ExecuteAsync(
            "UPDATE transcription_jobs SET summary = NULL " +
            "WHERE id = (SELECT job_id FROM transcript_segments WHERE id = @segmentId)",
            new { segmentId });
    }

    public async Task<int> DeleteByJobAsync(Guid jobId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return await conn.ExecuteAsync(
            "DELETE FROM transcript_segments WHERE job_id = @jobId",
            new { jobId });
    }

    public async Task<int> RenameSpeakerAsync(Guid jobId, string from, string to, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(
            "UPDATE transcript_segments SET speaker = @to WHERE job_id = @jobId AND speaker = @from",
            new { jobId, from, to });
        if (affected > 0)
        {
            await conn.ExecuteAsync(
                "UPDATE transcription_jobs SET summary = NULL WHERE id = @jobId",
                new { jobId });
        }
        return affected;
    }

    public async Task<IReadOnlyList<SearchHitDto>> SearchAsync(string query, int limit, SearchFilters? filters = null, CancellationToken ct = default)
    {
        var jobId = filters?.JobId;
        var from = filters?.From;
        var to = filters?.To;
        var where = "WHERE s.text_vector @@ plainto_tsquery('russian', @query) ";
        if (jobId is not null) { where += "AND s.job_id = @jobId "; }
        if (!string.IsNullOrWhiteSpace(filters?.Speaker)) { where += "AND s.speaker = @speaker "; }
        if (from is not null) { where += "AND j.created_at >= @from "; }
        if (to is not null) { where += "AND j.created_at < @to "; }
        if (!string.IsNullOrWhiteSpace(filters?.Model)) { where += "AND j.model = @model "; }

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<SearchHitDto>(
            "SELECT * FROM (" +
            "SELECT 'segment' AS Type, s.id AS SegmentId, s.job_id AS JobId, j.original_file_name AS OriginalFileName, " +
            "s.start_time AS StartTime, s.end_time AS EndTime, s.speaker AS Speaker, " +
            "ts_headline('russian', s.text, plainto_tsquery('russian', @query)) AS Snippet, " +
            "ts_rank(s.text_vector, plainto_tsquery('russian', @query)) AS Rank " +
            "FROM transcript_segments s " +
            "JOIN transcription_jobs j ON j.id = s.job_id " +
            where +
            "UNION ALL " +
            "SELECT 'frame', f.id, f.job_id, j.original_file_name, " +
            "f.time_seconds, f.time_seconds, NULL, " +
            "ts_headline('russian', f.ocr_text, plainto_tsquery('russian', @query)), " +
            "ts_rank(f.ocr_vector, plainto_tsquery('russian', @query)) " +
            "FROM media_frames f " +
            "JOIN transcription_jobs j ON j.id = f.job_id " +
            "WHERE f.ocr_vector @@ plainto_tsquery('russian', @query) " +
            (jobId is not null ? "AND f.job_id = @jobId " : "") +
            (from is not null ? "AND j.created_at >= @from " : "") +
            (to is not null ? "AND j.created_at < @to " : "") +
            (!string.IsNullOrWhiteSpace(filters?.Model) ? "AND j.model = @model " : "") +
            (!string.IsNullOrWhiteSpace(filters?.Speaker) ? "AND FALSE " : "") +
            ") hits ORDER BY Rank DESC LIMIT @limit",
            new
            {
                query,
                limit,
                jobId,
                speaker = filters?.Speaker,
                from,
                to,
                model = filters?.Model
            });
        return rows.ToList();
    }
}
