using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NUnit.Framework;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Ports;
using SmartTranscriber.Core.Services;

namespace SmartTranscriber.Tests;

[TestFixture]
public class JobRetryTests
{
    [Test]
    public async Task Retry_IngestFailedJob_GoesBackToDownloading()
    {
        var job = new TranscriptionJob
        {
            Id = Guid.NewGuid(),
            ObjectName = $"{TranscriptionJob.UrlObjectPrefix}deadbeef",
            OriginalFileName = "https://youtu.be/example",
            Status = JobStatus.Failed,
            ErrorMessage = "url ingest: yt-dlp exited 1",
            CompletedAt = DateTime.UtcNow
        };
        var (svc, repo, queue) = Build(job);

        var outcome = await svc.RetryJobAsync(job.Id);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(RetryOutcome.Reingesting));
            var stored = repo.Jobs[job.Id];
            Assert.That(stored.Status, Is.EqualTo(JobStatus.Downloading));
            Assert.That(stored.Progress, Is.EqualTo(2));
            Assert.That(stored.ErrorMessage, Is.Null);
            Assert.That(stored.CompletedAt, Is.Null);
            Assert.That(queue.Enqueued, Is.Empty);
        });
    }

    [Test]
    public async Task Retry_IngestFailedJob_ReingestEventuallyEnqueues()
    {
        var job = new TranscriptionJob
        {
            Id = Guid.NewGuid(),
            ObjectName = $"{TranscriptionJob.UrlObjectPrefix}deadbeef",
            OriginalFileName = "https://youtu.be/example",
            Status = JobStatus.Failed
        };
        var (svc, repo, queue) = Build(job);

        await svc.RetryJobAsync(job.Id);

        // background IngestUrlAsync: fake fetcher+storage succeed → Pending
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (repo.Jobs[job.Id].Status == JobStatus.Downloading && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Assert.Multiple(() =>
        {
            Assert.That(repo.Jobs[job.Id].Status, Is.EqualTo(JobStatus.Pending));
            Assert.That(repo.Jobs[job.Id].ObjectName,
                Does.Not.StartWith(TranscriptionJob.UrlObjectPrefix));
            Assert.That(queue.Enqueued, Does.Contain(job.Id));
        });
    }

    [Test]
    public async Task Retry_TranscribeFailedJob_GoesToPending()
    {
        var job = new TranscriptionJob
        {
            Id = Guid.NewGuid(),
            ObjectName = "abc_media.webm",
            OriginalFileName = "media.webm",
            Status = JobStatus.Failed,
            ErrorMessage = "stt exploded"
        };
        var (svc, repo, queue) = Build(job);

        var outcome = await svc.RetryJobAsync(job.Id);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(RetryOutcome.Queued));
            Assert.That(repo.Jobs[job.Id].Status, Is.EqualTo(JobStatus.Pending));
            Assert.That(repo.Jobs[job.Id].ErrorMessage, Is.Null);
            Assert.That(queue.Enqueued, Does.Contain(job.Id));
        });
    }

    [Test]
    public async Task Retry_UnknownJob_ReturnsNotFound()
    {
        var (svc, _, _) = Build();

        Assert.That(await svc.RetryJobAsync(Guid.NewGuid()), Is.EqualTo(RetryOutcome.NotFound));
    }

    [TestCase(JobStatus.Pending)]
    [TestCase(JobStatus.Downloading)]
    [TestCase(JobStatus.Processing)]
    [TestCase(JobStatus.Done)]
    public async Task Retry_NonFailedJob_ReturnsNotFailed(JobStatus status)
    {
        var job = new TranscriptionJob
        {
            Id = Guid.NewGuid(),
            ObjectName = "abc_media.webm",
            Status = status
        };
        var (svc, repo, queue) = Build(job);

        var outcome = await svc.RetryJobAsync(job.Id);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(RetryOutcome.NotFailed));
            Assert.That(repo.Jobs[job.Id].Status, Is.EqualTo(status));
            Assert.That(queue.Enqueued, Is.Empty);
        });
    }

    private static (JobService Svc, FakeJobRepo Repo, FakeQueue Queue) Build(params TranscriptionJob[] seed)
    {
        var repo = new FakeJobRepo(seed);
        var queue = new FakeQueue();
        var storage = new FakeStorage();
        var scopeFactory = new ServiceCollection()
            .AddSingleton<IJobRepository>(repo)
            .AddSingleton<IStorageService>(storage)
            .AddSingleton<IMediaFetcher>(new FakeFetcher())
            .AddSingleton<IJobQueue>(queue)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        var svc = new JobService(repo, storage, queue, scopeFactory,
            new FakeSegments(), new FakeWebhooks(), NullLogger<JobService>.Instance);
        return (svc, repo, queue);
    }

    private sealed class FakeJobRepo : IJobRepository
    {
        public readonly Dictionary<Guid, TranscriptionJob> Jobs;

        public FakeJobRepo(IEnumerable<TranscriptionJob> seed)
        {
            Jobs = seed.ToDictionary(j => j.Id);
        }

        public Task<TranscriptionJob?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(Jobs.GetValueOrDefault(id));
        }

        public Task<IReadOnlyList<TranscriptionJob>> ListAsync(JobStatus? status, string? tag, int limit, int offset, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<TranscriptionJob?> ClaimNextPendingAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<TranscriptionJob>> ResetInterruptedJobsAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task AddAsync(TranscriptionJob job, CancellationToken ct = default)
        {
            Jobs[job.Id] = job;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TranscriptionJob job, CancellationToken ct = default)
        {
            Jobs[job.Id] = job;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Jobs.Remove(id);
            return Task.CompletedTask;
        }

        public Task<JobStatsDto> GetStatsAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakeQueue : IJobQueue
    {
        public readonly List<Guid> Enqueued = new();

        public ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default)
        {
            Enqueued.Add(jobId);
            return ValueTask.CompletedTask;
        }

        public ValueTask<Guid> DequeueAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakeStorage : IStorageService
    {
        public Task EnsureBucketAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task UploadAsync(string objectName, Stream data, string contentType, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Stream> DownloadAsync(string objectName, CancellationToken ct = default) => Task.FromResult<Stream>(Stream.Null);
        public Task<bool> ExistsAsync(string objectName, CancellationToken ct = default) => Task.FromResult(true);
        public Task DeleteAsync(string objectName, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeFetcher : IMediaFetcher
    {
        public Task<string> FetchAsync(string url, string workDir, CancellationToken ct = default)
        {
            var path = Path.Combine(workDir, "media.webm");
            File.WriteAllText(path, "fake");
            return Task.FromResult(path);
        }
    }

    private sealed class FakeSegments : ISegmentRepository
    {
        public Task<IReadOnlyList<TranscriptSegment>> GetByJobAsync(Guid jobId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<TranscriptSegment>>(Array.Empty<TranscriptSegment>());
        }

        public Task AddRangeAsync(IEnumerable<TranscriptSegment> segments, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> UpdateSpeakerAsync(long segmentId, string? speaker, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> UpdateTextAsync(long segmentId, string text, CancellationToken ct = default) => Task.FromResult(true);
        public Task<int> DeleteByJobAsync(Guid jobId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> RenameSpeakerAsync(Guid jobId, string from, string to, CancellationToken ct = default) => Task.FromResult(0);
        public Task RemapSpeakersAsync(Guid jobId, IReadOnlyDictionary<string, string> map, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SearchHitDto>> SearchAsync(string query, int limit, SearchFilters? filters = null, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<SearchHitDto>>(Array.Empty<SearchHitDto>());
        }
    }

    private sealed class FakeWebhooks : IWebhookNotifier
    {
        public Task NotifyJobFinishedAsync(TranscriptionJob job, CancellationToken ct = default) => Task.CompletedTask;
    }
}
