using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NUnit.Framework;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Ports;
using SmartTranscriber.Core.Services;

namespace SmartTranscriber.Tests;

[TestFixture]
public class JobCancelTests
{
    [TestCase(JobStatus.Pending)]
    [TestCase(JobStatus.Downloading)]
    [TestCase(JobStatus.Processing)]
    public async Task Cancel_LiveJob_MarksCancelled(JobStatus status)
    {
        var job = new TranscriptionJob
        {
            Id = Guid.NewGuid(),
            ObjectName = "abc_media.webm",
            Status = status
        };
        var (svc, repo, _, webhooks) = Build(job);

        var result = await svc.CancelJobAsync(job.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            var stored = repo.Jobs[job.Id];
            Assert.That(stored.Status, Is.EqualTo(JobStatus.Cancelled));
            Assert.That(stored.CompletedAt, Is.Not.Null);
            Assert.That(webhooks.Notified, Does.Contain(job.Id));
        });
    }

    [TestCase(JobStatus.Done)]
    [TestCase(JobStatus.Failed)]
    [TestCase(JobStatus.Cancelled)]
    public async Task Cancel_TerminalJob_ReturnsFalse(JobStatus status)
    {
        var job = new TranscriptionJob
        {
            Id = Guid.NewGuid(),
            ObjectName = "abc_media.webm",
            Status = status
        };
        var (svc, repo, _, webhooks) = Build(job);

        var result = await svc.CancelJobAsync(job.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(repo.Jobs[job.Id].Status, Is.EqualTo(status));
            Assert.That(webhooks.Notified, Is.Empty);
        });
    }

    [Test]
    public async Task Cancel_UnknownJob_ReturnsNull()
    {
        var (svc, _, _, _) = Build();

        Assert.That(await svc.CancelJobAsync(Guid.NewGuid()), Is.Null);
    }

    [Test]
    public async Task Cancel_SignalsRegisteredToken()
    {
        var job = new TranscriptionJob
        {
            Id = Guid.NewGuid(),
            ObjectName = "abc_media.webm",
            Status = JobStatus.Processing
        };
        var (svc, _, _, _) = Build(new[] { job }, out var registry);
        var cts = registry.Register(job.Id);

        await svc.CancelJobAsync(job.Id);

        Assert.That(cts.IsCancellationRequested, Is.True);
    }

    [Test]
    public async Task Retry_CancelledJob_IsAccepted()
    {
        var job = new TranscriptionJob
        {
            Id = Guid.NewGuid(),
            ObjectName = "abc_media.webm",
            OriginalFileName = "media.webm",
            Status = JobStatus.Cancelled
        };
        var (svc, repo, queue, _) = Build(job);

        var outcome = await svc.RetryJobAsync(job.Id);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(RetryOutcome.Queued));
            Assert.That(repo.Jobs[job.Id].Status, Is.EqualTo(JobStatus.Pending));
            Assert.That(queue.Enqueued, Does.Contain(job.Id));
        });
    }

    [Test]
    public async Task Retry_CancelledIngestJob_Reingests()
    {
        var job = new TranscriptionJob
        {
            Id = Guid.NewGuid(),
            ObjectName = $"{TranscriptionJob.UrlObjectPrefix}deadbeef",
            OriginalFileName = "https://youtu.be/example",
            Status = JobStatus.Cancelled
        };
        var (svc, repo, _, _) = Build(job);

        var outcome = await svc.RetryJobAsync(job.Id);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(RetryOutcome.Reingesting));
            Assert.That(repo.Jobs[job.Id].Status, Is.EqualTo(JobStatus.Downloading));
        });
    }

    private static (JobService Svc, FakeJobRepo Repo, FakeQueue Queue, FakeWebhooks Webhooks)
        Build(params TranscriptionJob[] seed)
    {
        return Build(seed, out _);
    }

    private static (JobService Svc, FakeJobRepo Repo, FakeQueue Queue, FakeWebhooks Webhooks)
        Build(TranscriptionJob[] seed, out JobCancellationRegistry registry)
    {
        var repo = new FakeJobRepo(seed);
        var queue = new FakeQueue();
        var webhooks = new FakeWebhooks();
        registry = new JobCancellationRegistry();
        var scopeFactory = new ServiceCollection()
            .AddSingleton<IJobRepository>(repo)
            .AddSingleton<IStorageService>(new FakeStorage())
            .AddSingleton<IMediaFetcher>(new FakeFetcher())
            .AddSingleton<IJobQueue>(queue)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        var svc = new JobService(repo, new FakeStorage(), queue, scopeFactory,
            new FakeSegments(), webhooks, registry, NullLogger<JobService>.Instance);
        return (svc, repo, queue, webhooks);
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
        public readonly List<Guid> Notified = new();

        public Task NotifyJobFinishedAsync(TranscriptionJob job, CancellationToken ct = default)
        {
            Notified.Add(job.Id);
            return Task.CompletedTask;
        }
    }
}
