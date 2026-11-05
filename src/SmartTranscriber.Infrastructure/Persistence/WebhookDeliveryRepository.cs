using Dapper;

using Npgsql;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Persistence;

public sealed class WebhookDeliveryRepository(NpgsqlDataSource dataSource) : IWebhookDeliveryRepository
{
    private const string Columns =
        "id, job_id AS JobId, url, event, status_code AS StatusCode, success, error, " +
        "duration_ms AS DurationMs, created_at AS CreatedAt";

    public async Task AddRangeAsync(IEnumerable<WebhookDelivery> deliveries, CancellationToken ct = default)
    {
        var list = deliveries.ToList();
        if (list.Count == 0)
        {
            return;
        }
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            "INSERT INTO webhook_deliveries (job_id, url, event, status_code, success, error, duration_ms, created_at) " +
            "VALUES (@JobId, @Url, @Event, @StatusCode, @Success, @Error, @DurationMs, @CreatedAt)",
            list);
    }

    public async Task<IReadOnlyList<WebhookDelivery>> ListAsync(Guid? jobId, int limit, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<WebhookDelivery>(
            $"SELECT {Columns} FROM webhook_deliveries " +
            (jobId is null ? "" : "WHERE job_id = @jobId ") +
            "ORDER BY id DESC LIMIT @limit",
            new { jobId, limit });
        return rows.ToList();
    }
}
