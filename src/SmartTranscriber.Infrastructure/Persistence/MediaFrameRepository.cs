using Dapper;
using Npgsql;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Persistence;

public sealed class MediaFrameRepository(NpgsqlDataSource dataSource) : IFrameRepository
{
    public async Task InsertBatchAsync(IReadOnlyList<MediaFrame> frames, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        foreach (var f in frames)
        {
            await conn.ExecuteAsync(
                "INSERT INTO media_frames (job_id, frame_no, time_seconds, object_name) " +
                "VALUES (@JobId, @FrameNo, @TimeSeconds, @ObjectName) " +
                "ON CONFLICT (job_id, frame_no) DO NOTHING",
                f);
        }
    }

    public async Task<IReadOnlyList<MediaFrame>> GetByJobAsync(Guid jobId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<MediaFrame>(
            "SELECT id AS Id, job_id AS JobId, frame_no AS FrameNo, " +
            "time_seconds AS TimeSeconds, object_name AS ObjectName " +
            "FROM media_frames WHERE job_id = @jobId ORDER BY frame_no",
            new { jobId });
        return rows.ToList();
    }

    public async Task<MediaFrame?> GetAsync(Guid jobId, int frameNo, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<MediaFrame>(
            "SELECT id AS Id, job_id AS JobId, frame_no AS FrameNo, " +
            "time_seconds AS TimeSeconds, object_name AS ObjectName " +
            "FROM media_frames WHERE job_id = @jobId AND frame_no = @frameNo",
            new { jobId, frameNo });
    }

    public async Task UpdateOcrAsync(Guid jobId, int frameNo, string ocrText, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE media_frames SET ocr_text = @ocrText WHERE job_id = @jobId AND frame_no = @frameNo",
            new { jobId, frameNo, ocrText });
    }
}
