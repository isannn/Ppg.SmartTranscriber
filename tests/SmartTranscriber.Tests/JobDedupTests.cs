using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NUnit.Framework;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Ports;
using SmartTranscriber.Core.Services;

namespace SmartTranscriber.Tests;

[TestFixture]
public class JobDedupTests
{
    private FakeJobRepo _jobs = null!;
    private FakeSegRepo _segs = null!;
    private FakeStorage _store = null!;
    private FakeQueue _queue = null!;
    private FakeWebhooks _hooks = null!;
    private FakeFetcher _fetcher = null!;
    private JobService _svc = null!;

    [SetUp]
    public void SetUp()
    {
        _jobs = new FakeJobRepo();
        _segs = new FakeSegRepo();
        _store = new FakeStorage();
        _queue = new FakeQueue();
        _hooks = new FakeWebhooks();
        _fetcher = new FakeFetcher();
        var scopes = new FakeScopeFactory(new Dictionary<Type, object>
        {
            [typeof(IJobRepository)] = _jobs,
            [typeof(ISegmentRepository)] = _segs,
            [typeof(IStorageService)] = _store,
            [typeof(IJobQueue)] = _queue,
            [typeof(IMediaFetcher)] = _fetcher,
            [typeof(IWebhookNotifier)] = _hooks
        });
        _svc = new JobService(_jobs, _store, _queue, scopes, _segs, _hooks, NullLogger<JobService>.Instance);
    }

    [Test]
    public async Task Upload_SameBytesTwice_SecondClonesResults()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var first = await _svc.CreateJobAsync("a.mp4", new MemoryStream(bytes), "video/mp4");
        var j1 = _jobs.Store.Single(j => j.Id == first.Id);
        j1.Status = JobStatus.Done;
        j1.CompletedAt = DateTime.UtcNow;
        _segs.Store.Add(new TranscriptSegment { JobId = j1.Id, StartTime = 0, EndTime = 1, Text = "hi" });

        var second = await _svc.CreateJobAsync("b.mp4", new MemoryStream(bytes), "video/mp4");

        Assert.Multiple(() =>
        {
            Assert.That(second.Status, Is.EqualTo("Done"));
            Assert.That(second.DuplicateOf, Is.EqualTo(j1.Id));
            Assert.That(second.Duplicate, Is.True);
            Assert.That(second.ObjectName, Is.EqualTo(j1.ObjectName));
            Assert.That(second.SegmentCount, Is.EqualTo(1));
        });
        var copied = _segs.Store.Where(s => s.JobId == second.Id).ToList();
        Assert.That(copied, Has.Count.EqualTo(1));
        Assert.That(copied[0].Text, Is.EqualTo("hi"));
        Assert.That(_store.Objects, Has.Count.EqualTo(1), "clone shares the canonical object, no re-upload");
        Assert.That(_queue.Enqueued, Has.Count.EqualTo(1), "clone is not enqueued for transcription");
        Assert.That(_hooks.Notified, Does.Contain(second.Id));
    }

    [Test]
    public async Task Upload_DifferentBytes_BothJobsAreReal()
    {
        var a = await _svc.CreateJobAsync("a.mp4", new MemoryStream(new byte[] { 1 }), "video/mp4");
        var b = await _svc.CreateJobAsync("b.mp4", new MemoryStream(new byte[] { 2 }), "video/mp4");

        Assert.That(a.Status, Is.EqualTo("Pending"));
        Assert.That(b.DuplicateOf, Is.Null);
        Assert.That(_store.Objects, Has.Count.EqualTo(2));
        Assert.That(_queue.Enqueued, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task FromUrl_InFlightOwner_ReturnsExistingJob()
    {
        var seed = Seed("youtube.com/watch?v=x", JobStatus.Downloading);
        var dto = await _svc.CreateUrlJobAsync("https://youtu.be/x");

        Assert.Multiple(() =>
        {
            Assert.That(dto.Id, Is.EqualTo(seed.Id));
            Assert.That(dto.Duplicate, Is.True);
            Assert.That(_jobs.Store, Has.Count.EqualTo(1), "no new row for an in-flight URL hit");
        });
    }

    [Test]
    public async Task FromUrl_DoneOwner_ClonesIntoNewJob()
    {
        var seed = Seed("youtube.com/watch?v=x", JobStatus.Done);
        seed.ContentHash = "deadbeef";
        seed.Summary = "sum";
        _segs.Store.Add(new TranscriptSegment { JobId = seed.Id, StartTime = 0, EndTime = 2, Text = "s" });

        var dto = await _svc.CreateUrlJobAsync("https://www.youtube.com/watch?v=x&si=1");

        var clone = _jobs.Store.Single(j => j.Id == dto.Id);
        Assert.Multiple(() =>
        {
            Assert.That(dto.Id, Is.Not.EqualTo(seed.Id));
            Assert.That(clone.Status, Is.EqualTo(JobStatus.Done));
            Assert.That(clone.DuplicateOf, Is.EqualTo(seed.Id));
            Assert.That(clone.SourceUrl, Is.Null, "canonical keeps the live URL claim");
            Assert.That(clone.ObjectName, Is.EqualTo(seed.ObjectName));
            Assert.That(clone.Summary, Is.EqualTo("sum"));
            Assert.That(_segs.Store.Count(s => s.JobId == clone.Id), Is.EqualTo(1));
            Assert.That(_hooks.Notified, Does.Contain(clone.Id));
        });
    }

    [Test]
    public async Task FromUrl_FailedOwner_CreatesFreshJob()
    {
        Seed("youtube.com/watch?v=x", JobStatus.Failed);
        _fetcher.Bytes = new byte[] { 9 };

        var dto = await _svc.CreateUrlJobAsync("https://youtu.be/x");

        Assert.Multiple(() =>
        {
            Assert.That(dto.DuplicateOf, Is.Null);
            Assert.That(_jobs.Store, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task FromUrl_SameContent_DedupsAfterDownload()
    {
        var canonical = Seed("other.example/v", JobStatus.Done);
        canonical.ContentHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(new byte[] { 7, 7 })).ToLowerInvariant();
        _fetcher.Bytes = new byte[] { 7, 7 };

        var dto = await _svc.CreateUrlJobAsync("https://different.example/watch?v=zzz");

        var job = _jobs.Store.Single(j => j.Id == dto.Id);
        await WaitFor(() => job.Status == JobStatus.Done);
        Assert.Multiple(() =>
        {
            Assert.That(job.DuplicateOf, Is.EqualTo(canonical.Id));
            Assert.That(job.ObjectName, Is.EqualTo(canonical.ObjectName));
            Assert.That(_store.Objects, Is.Empty, "content hit skips the S3 upload");
        });
    }

    [Test]
    public async Task DeleteJob_SharedObject_IsKeptWhileReferenced()
    {
        var canonical = Seed(null, JobStatus.Done);
        canonical.ObjectName = "shared_obj";
        _store.Objects["shared_obj"] = [1];
        var clone = Seed(null, JobStatus.Done);
        clone.ObjectName = "shared_obj";
        clone.DuplicateOf = canonical.Id;

        await _svc.DeleteJobAsync(clone.Id, deleteFile: true);
        Assert.That(_store.Objects, Contains.Key("shared_obj"));

        await _svc.DeleteJobAsync(canonical.Id, deleteFile: true);
        Assert.That(_store.Objects, Is.Empty);
    }

    [Test]
    public async Task Retry_FailedJob_UrlReclaimed_ClearsSourceUrl()
    {
        var failed = Seed("youtube.com/watch?v=x", JobStatus.Failed);
        Seed("youtube.com/watch?v=x", JobStatus.Pending);

        var retried = await _svc.RetryJobAsync(failed.Id);

        Assert.Multiple(() =>
        {
            Assert.That(retried, Is.True);
            Assert.That(failed.SourceUrl, Is.Null);
            Assert.That(failed.Status, Is.EqualTo(JobStatus.Pending));
        });
    }

    private TranscriptionJob Seed(string? sourceUrl, JobStatus status)
    {
        var j = new TranscriptionJob
        {
            Id = Guid.NewGuid(),
            ObjectName = $"obj_{Guid.NewGuid():N}",
            OriginalFileName = "seed",
            Status = status,
            CreatedAt = DateTime.UtcNow,
            SourceUrl = sourceUrl
        };
        _jobs.Store.Add(j);
        return j;
    }

    private static async Task WaitFor(Func<bool> cond, int ms = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (!cond() && DateTime.UtcNow < until)
        {
            await Task.Delay(25);
        }
        Assert.That(cond(), Is.True, "timed out waiting for condition");
    }

    private sealed class FakeJobRepo : IJobRepository
    {
        public List<TranscriptionJob> Store { get; } = new();
        private readonly object _lock = new();

        public Task<TranscriptionJob?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Store.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<TranscriptionJob>> ListAsync(JobStatus? status, string? tag, int limit, int offset, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TranscriptionJob>>(Store.ToList());

        public Task<TranscriptionJob?> ClaimNextPendingAsync(CancellationToken ct = default)
            => Task.FromResult(Store.FirstOrDefault(j => j.Status == JobStatus.Pending));

        public Task<IReadOnlyList<TranscriptionJob>> ResetInterruptedJobsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TranscriptionJob>>(new List<TranscriptionJob>());

        public Task AddAsync(TranscriptionJob job, CancellationToken ct = default)
        {
            Store.Add(job);
            return Task.CompletedTask;
        }

        public Task<(TranscriptionJob Job, bool Created)> GetOrAddUrlJobAsync(TranscriptionJob job, CancellationToken ct = default)
        {
            lock (_lock)
            {
                var owner = Store.FirstOrDefault(j => j.SourceUrl == job.SourceUrl && j.Status != JobStatus.Failed);
                if (owner is not null)
                {
                    return Task.FromResult((owner, false));
                }
                Store.Add(job);
                return Task.FromResult((job, true));
            }
        }

        public Task<TranscriptionJob?> FindDoneByContentHashAsync(string contentHash, Guid excludeId, CancellationToken ct = default)
            => Task.FromResult(Store.FirstOrDefault(j =>
                j.ContentHash == contentHash && j.Status == JobStatus.Done && j.Id != excludeId));

        public Task<int> CountByObjectNameAsync(string objectName, Guid excludeId, CancellationToken ct = default)
            => Task.FromResult(Store.Count(j => j.ObjectName == objectName && j.Id != excludeId));

        public Task<TranscriptionJob?> FindUrlOwnerAsync(string sourceUrl, Guid excludeId, CancellationToken ct = default)
            => Task.FromResult(Store.FirstOrDefault(j =>
                j.SourceUrl == sourceUrl && j.Status != JobStatus.Failed && j.Id != excludeId));

        public Task UpdateAsync(TranscriptionJob job, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Store.RemoveAll(j => j.Id == id);
            return Task.CompletedTask;
        }

        public Task<JobStatsDto> GetStatsAsync(CancellationToken ct = default)
            => Task.FromResult(new JobStatsDto(0, new Dictionary<string, int>(),
                new List<JobStatsModel>(), new List<JobStatsDay>(), 0));
    }

    private sealed class FakeSegRepo : ISegmentRepository
    {
        public List<TranscriptSegment> Store { get; } = new();

        public Task<IReadOnlyList<TranscriptSegment>> GetByJobAsync(Guid jobId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TranscriptSegment>>(Store.Where(s => s.JobId == jobId).ToList());

        public Task AddRangeAsync(IEnumerable<TranscriptSegment> segments, CancellationToken ct = default)
        {
            Store.AddRange(segments);
            return Task.CompletedTask;
        }

        public Task<bool> UpdateSpeakerAsync(long segmentId, string? speaker, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> UpdateTextAsync(long segmentId, string text, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> DeleteByJobAsync(Guid jobId, CancellationToken ct = default)
        {
            var n = Store.RemoveAll(s => s.JobId == jobId);
            return Task.FromResult(n);
        }

        public Task<int> RenameSpeakerAsync(Guid jobId, string from, string to, CancellationToken ct = default) => Task.FromResult(0);
        public Task RemapSpeakersAsync(Guid jobId, IReadOnlyDictionary<string, string> map, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SearchHitDto>> SearchAsync(string query, int limit, SearchFilters? filters = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SearchHitDto>>(new List<SearchHitDto>());
    }

    private sealed class FakeStorage : IStorageService
    {
        public Dictionary<string, byte[]> Objects { get; } = new();
        public Task EnsureBucketAsync(CancellationToken ct = default) => Task.CompletedTask;
        public async Task UploadAsync(string objectName, Stream data, string contentType, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            await data.CopyToAsync(ms, ct);
            Objects[objectName] = ms.ToArray();
        }

        public Task<Stream> DownloadAsync(string objectName, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream(Objects[objectName]));

        public Task<bool> ExistsAsync(string objectName, CancellationToken ct = default)
            => Task.FromResult(Objects.ContainsKey(objectName));

        public Task DeleteAsync(string objectName, CancellationToken ct = default)
        {
            Objects.Remove(objectName);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeQueue : IJobQueue
    {
        public List<Guid> Enqueued { get; } = new();
        public ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default)
        {
            Enqueued.Add(jobId);
            return ValueTask.CompletedTask;
        }

        public ValueTask<Guid> DequeueAsync(CancellationToken ct = default) => new(Guid.Empty);
    }

    private sealed class FakeWebhooks : IWebhookNotifier
    {
        public List<Guid> Notified { get; } = new();
        public Task NotifyJobFinishedAsync(TranscriptionJob job, CancellationToken ct = default)
        {
            Notified.Add(job.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFetcher : IMediaFetcher
    {
        public byte[] Bytes { get; set; } = [1];
        public Task<string> FetchAsync(string url, string workDir, CancellationToken ct = default)
        {
            var path = Path.Combine(workDir, "media.bin");
            File.WriteAllBytes(path, Bytes);
            return Task.FromResult(path);
        }
    }

    private sealed class FakeScopeFactory(Dictionary<Type, object> svc) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeScope(svc);
    }

    private sealed class FakeScope(Dictionary<Type, object> svc) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new FakeProvider(svc);
        public void Dispose() { }
    }

    private sealed class FakeProvider(Dictionary<Type, object> svc) : IServiceProvider
    {
        public object? GetService(Type serviceType) => svc.GetValueOrDefault(serviceType);
    }
}
