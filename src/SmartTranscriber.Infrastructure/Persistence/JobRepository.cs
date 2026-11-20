using Dapper;

using Npgsql;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Persistence;

public sealed class JobRepository(NpgsqlDataSource dataSource) : IJobRepository
{
    private const string JobColumns =
        "id, object_name AS ObjectName, original_file_name AS OriginalFileName, status, " +
        "created_at AS CreatedAt, completed_at AS CompletedAt, error_message AS ErrorMessage, " +
        "model, language, duration_seconds AS DurationSeconds, summary AS Summary, progress AS Progress, " +
        "callback_url AS CallbackUrl, diarize AS Diarize, hotwords AS Hotwords, priority AS Priority, tags AS Tags, " +
        "translation AS Translation, translation_lang AS TranslationLang, media_meta AS MediaMeta, " +
        "source_url AS SourceUrl, content_hash AS ContentHash, duplicate_of AS DuplicateOf";

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

    public async Task<IReadOnlyList<TranscriptionJob>> ListAsync(JobStatus? status, string? tag, int limit, int offset, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var conditions = new List<string>();
        if (status is not null)
        {
            conditions.Add("status = @status");
        }
        if (!string.IsNullOrWhiteSpace(tag))
        {
            conditions.Add("(',' || coalesce(tags, '') || ',') LIKE '%,' || @tag || ',%'");
        }
        var sql = $"SELECT {JobColumns}, " +
                  "(SELECT count(*) FROM transcript_segments s WHERE s.job_id = j.id) AS segment_count " +
                  "FROM transcription_jobs j " +
                  (conditions.Count == 0 ? "" : "WHERE " + string.Join(" AND ", conditions) + " ") +
                  "ORDER BY created_at DESC LIMIT @limit OFFSET @offset";

        var rows = await conn.QueryAsync<JobRow>(sql, new { status = status?.ToString(), tag, limit, offset });
        return rows.Select(r => r.ToJob()).ToList();
    }

    public async Task<TranscriptionJob?> ClaimNextPendingAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<TranscriptionJob>(
            $"UPDATE transcription_jobs SET status = 'Processing' " +
            "WHERE id = (SELECT id FROM transcription_jobs WHERE status = 'Pending' " +
            "ORDER BY priority DESC, created_at FOR UPDATE SKIP LOCKED LIMIT 1) " +
            $"RETURNING {JobColumns}");
    }

    public async Task<IReadOnlyList<TranscriptionJob>> ResetInterruptedJobsAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var requeued = (await conn.QueryAsync<TranscriptionJob>(
            $"UPDATE transcription_jobs SET status = 'Pending', progress = 0 " +
            $"WHERE status = 'Processing' RETURNING {JobColumns}")).ToList();
        var failed = (await conn.QueryAsync<TranscriptionJob>(
            $"UPDATE transcription_jobs SET status = 'Failed', " +
            $"error_message = 'url ingest interrupted by restart', completed_at = now() " +
            $"WHERE status = 'Downloading' RETURNING {JobColumns}")).ToList();
        return requeued.Concat(failed).ToList();
    }

    public async Task AddAsync(TranscriptionJob job, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            "INSERT INTO transcription_jobs " +
            "(id, object_name, original_file_name, status, created_at, completed_at, error_message, model, language, duration_seconds, summary, callback_url, diarize, hotwords, priority, tags, translation, translation_lang, media_meta, source_url, content_hash, duplicate_of) " +
            "VALUES (@Id, @ObjectName, @OriginalFileName, @Status, @CreatedAt, @CompletedAt, @ErrorMessage, @Model, @Language, @DurationSeconds, @Summary, @CallbackUrl, @Diarize, @Hotwords, @Priority, @Tags, @Translation, @TranslationLang, @MediaMeta, @SourceUrl, @ContentHash, @DuplicateOf)",
            new
            {
                job.Id,
                job.ObjectName,
                job.OriginalFileName,
                Status = job.Status.ToString(),
                job.CreatedAt,
                job.CompletedAt,
                job.ErrorMessage,
                job.Model,
                job.Language,
                job.DurationSeconds,
                job.Summary,
                job.CallbackUrl,
                job.Diarize,
                job.Hotwords,
                job.Priority,
                job.Tags,
                job.Translation,
                job.TranslationLang,
                job.MediaMeta,
                job.SourceUrl,
                job.ContentHash,
                job.DuplicateOf
            });
    }

    public async Task UpdateAsync(TranscriptionJob job, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE transcription_jobs SET status = @Status, completed_at = @CompletedAt, " +
            "error_message = @ErrorMessage, model = @Model, language = @Language, " +
            "duration_seconds = @DurationSeconds, summary = @Summary, progress = @Progress, " +
            "object_name = @ObjectName, original_file_name = @OriginalFileName, callback_url = @CallbackUrl, diarize = @Diarize, hotwords = @Hotwords, priority = @Priority, tags = @Tags, translation = @Translation, translation_lang = @TranslationLang, media_meta = @MediaMeta, source_url = @SourceUrl, content_hash = @ContentHash, duplicate_of = @DuplicateOf WHERE id = @Id",
            new
            {
                job.Id,
                Status = job.Status.ToString(),
                job.CompletedAt,
                job.ErrorMessage,
                job.Model,
                job.Language,
                job.DurationSeconds,
                job.Summary,
                job.Progress,
                job.ObjectName,
                job.OriginalFileName,
                job.CallbackUrl,
                job.Diarize,
                job.Hotwords,
                job.Priority,
                job.Tags,
                job.Translation,
                job.TranslationLang,
                job.MediaMeta,
                job.SourceUrl,
                job.ContentHash,
                job.DuplicateOf
            });
    }

    public async Task<(TranscriptionJob Job, bool Created)> GetOrAddUrlJobAsync(TranscriptionJob job, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        // A live owner may flip to Failed between the conflict and the
        // re-select — retry the insert, it succeeds once the index
        // stops covering that row.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var inserted = await conn.ExecuteScalarAsync<Guid?>(
                "INSERT INTO transcription_jobs " +
                "(id, object_name, original_file_name, status, created_at, completed_at, error_message, model, language, duration_seconds, summary, callback_url, diarize, hotwords, priority, tags, translation, translation_lang, media_meta, source_url, content_hash, duplicate_of) " +
                "VALUES (@Id, @ObjectName, @OriginalFileName, @Status, @CreatedAt, @CompletedAt, @ErrorMessage, @Model, @Language, @DurationSeconds, @Summary, @CallbackUrl, @Diarize, @Hotwords, @Priority, @Tags, @Translation, @TranslationLang, @MediaMeta, @SourceUrl, @ContentHash, @DuplicateOf) " +
                "ON CONFLICT (source_url) WHERE source_url IS NOT NULL AND status <> 'Failed' DO NOTHING RETURNING id",
                JobParams(job));
            if (inserted is not null)
            {
                return (job, true);
            }
            var owner = await FindBySourceUrlAsync(conn, job.SourceUrl!);
            if (owner is not null && owner.Status != JobStatus.Failed)
            {
                return (owner, false);
            }
        }
        throw new InvalidOperationException($"source_url {job.SourceUrl} stays contended");
    }

    private static async Task<TranscriptionJob?> FindBySourceUrlAsync(NpgsqlConnection conn, string sourceUrl)
    {
        var row = await conn.QuerySingleOrDefaultAsync<JobRow>(
            $"SELECT {JobColumns}, " +
            "(SELECT count(*) FROM transcript_segments s WHERE s.job_id = j.id) AS segment_count " +
            "FROM transcription_jobs j WHERE source_url = @sourceUrl ORDER BY created_at DESC LIMIT 1",
            new { sourceUrl });
        return row?.ToJob();
    }

    public async Task<TranscriptionJob?> FindDoneByContentHashAsync(string contentHash, Guid excludeId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<JobRow>(
            $"SELECT {JobColumns}, " +
            "(SELECT count(*) FROM transcript_segments s WHERE s.job_id = j.id) AS segment_count " +
            "FROM transcription_jobs j WHERE content_hash = @contentHash AND status = 'Done' AND id <> @excludeId " +
            "ORDER BY completed_at DESC LIMIT 1",
            new { contentHash, excludeId });
        return row?.ToJob();
    }

    public async Task<int> CountByObjectNameAsync(string objectName, Guid excludeId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM transcription_jobs WHERE object_name = @objectName AND id <> @excludeId",
            new { objectName, excludeId });
    }

    public async Task<TranscriptionJob?> FindUrlOwnerAsync(string sourceUrl, Guid excludeId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<JobRow>(
            $"SELECT {JobColumns}, " +
            "(SELECT count(*) FROM transcript_segments s WHERE s.job_id = j.id) AS segment_count " +
            "FROM transcription_jobs j WHERE source_url = @sourceUrl AND status <> 'Failed' AND id <> @excludeId " +
            "ORDER BY created_at DESC LIMIT 1",
            new { sourceUrl, excludeId });
        return row?.ToJob();
    }

    private static object JobParams(TranscriptionJob job)
    {
        return new
        {
            job.Id,
            job.ObjectName,
            job.OriginalFileName,
            Status = job.Status.ToString(),
            job.CreatedAt,
            job.CompletedAt,
            job.ErrorMessage,
            job.Model,
            job.Language,
            job.DurationSeconds,
            job.Summary,
            job.CallbackUrl,
            job.Diarize,
            job.Hotwords,
            job.Priority,
            job.Tags,
            job.Translation,
            job.TranslationLang,
            job.MediaMeta,
            job.SourceUrl,
            job.ContentHash,
            job.DuplicateOf
        };
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync("DELETE FROM transcription_jobs WHERE id = @id", new { id });
    }

    public async Task<JobStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var total = await conn.ExecuteScalarAsync<int>("SELECT count(*) FROM transcription_jobs");
        var byStatus = (await conn.QueryAsync<StatusCount>(
                "SELECT status AS Status, count(*) AS Count FROM transcription_jobs GROUP BY status"))
            .ToDictionary(r => r.Status, r => r.Count);
        var byModel = (await conn.QueryAsync<JobStatsModel>(
                "SELECT model AS Model, count(*) AS Jobs, coalesce(avg(duration_seconds), 0) AS AvgDurationSeconds " +
                "FROM transcription_jobs WHERE status = 'Done' GROUP BY model ORDER BY Jobs DESC")).ToList();
        var byDay = (await conn.QueryAsync<JobStatsDay>(
                "SELECT to_char(created_at, 'YYYY-MM-DD') AS Date, count(*) AS Jobs " +
                "FROM transcription_jobs GROUP BY 1 ORDER BY 1 DESC LIMIT 30")).ToList();
        var totalDuration = await conn.ExecuteScalarAsync<double>(
            "SELECT coalesce(sum(duration_seconds), 0) FROM transcription_jobs WHERE status = 'Done'");
        return new JobStatsDto(total, byStatus, byModel, byDay, totalDuration);
    }

    private sealed class StatusCount
    {
        public string Status { get; set; } = "";
        public int Count { get; set; }
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
        public string? Summary { get; set; }
        public int Progress { get; set; }
        public string? CallbackUrl { get; set; }
        public bool Diarize { get; set; } = true;
        public string? Hotwords { get; set; }
        public int Priority { get; set; }
        public string? Tags { get; set; }
        public string? Translation { get; set; }
        public string? TranslationLang { get; set; }
        public string? MediaMeta { get; set; }
        public string? SourceUrl { get; set; }
        public string? ContentHash { get; set; }
        public Guid? DuplicateOf { get; set; }
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
                Summary = Summary,
                Progress = Progress,
                CallbackUrl = CallbackUrl,
                Diarize = Diarize,
                Hotwords = Hotwords,
                Priority = Priority,
                Tags = Tags,
                Translation = Translation,
                TranslationLang = TranslationLang,
                MediaMeta = MediaMeta,
                SourceUrl = SourceUrl,
                ContentHash = ContentHash,
                DuplicateOf = DuplicateOf,
                SegmentCount = SegmentCount
            };
        }
    }
}
