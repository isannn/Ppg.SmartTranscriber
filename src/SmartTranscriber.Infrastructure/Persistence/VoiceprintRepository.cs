using System.Text.Json;

using Dapper;

using Npgsql;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Persistence;

public sealed class VoiceprintRepository(NpgsqlDataSource dataSource) : IVoiceprintRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<Voiceprint>> ListAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<VoiceprintRow>(
            "SELECT id, name, embedding, source_job_id AS SourceJobId, created_at AS CreatedAt " +
            "FROM speaker_voiceprints ORDER BY name");
        return rows.Select(r => r.ToVoiceprint()).ToList();
    }

    public async Task UpsertAsync(string name, double[] embedding, Guid? sourceJobId, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(embedding, JsonOpts);
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            "INSERT INTO speaker_voiceprints (name, embedding, source_job_id, created_at) " +
            "VALUES (@name, @embedding, @sourceJobId, now()) " +
            "ON CONFLICT (name) DO UPDATE SET embedding = EXCLUDED.embedding, " +
            "source_job_id = EXCLUDED.source_job_id, created_at = now()",
            new { name, embedding = json, sourceJobId });
    }

    private sealed class VoiceprintRow
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Embedding { get; set; } = "[]";
        public Guid? SourceJobId { get; set; }
        public DateTime CreatedAt { get; set; }

        public Voiceprint ToVoiceprint()
        {
            double[] emb;
            try
            {
                emb = JsonSerializer.Deserialize<double[]>(Embedding, JsonOpts) ?? [];
            }
            catch (JsonException)
            {
                emb = [];
            }
            return new Voiceprint
            {
                Id = Id,
                Name = Name,
                Embedding = emb,
                SourceJobId = SourceJobId,
                CreatedAt = CreatedAt
            };
        }
    }
}
