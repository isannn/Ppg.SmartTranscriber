using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SmartTranscriber.Core.Options;

namespace SmartTranscriber.Infrastructure.Live;

/// <summary>In-memory registry of live sessions + their temp PCM files.</summary>
public sealed class LiveSessionManager(IOptions<SmartTranscriberOptions> options, ILogger<LiveSessionManager> logger)
{
    private readonly ConcurrentDictionary<Guid, LiveSession> _sessions = new();

    public LiveSession Register(LiveSession session)
    {
        var live = options.Value.Live;
        var dir = string.IsNullOrWhiteSpace(options.Value.Worker.TempDir)
            ? Path.GetTempPath()
            : options.Value.Worker.TempDir;
        Directory.CreateDirectory(dir);
        session.TempPath = Path.Combine(dir, $"st-live-{session.Id:N}.pcm");
        File.WriteAllBytes(session.TempPath, []);
        _sessions[session.Id] = session;
        logger.LogInformation("Live session {SessionId} -> job {JobId} ({Path})", session.Id, session.JobId, session.TempPath);
        return session;
    }

    public LiveSession? Get(Guid id) => _sessions.TryGetValue(id, out var s) ? s : null;

    public IReadOnlyList<LiveSession> ActiveSessions() =>
        _sessions.Values.Where(s => s.Status is "active" or "finishing").ToList();

    /// <summary>Append raw PCM bytes; returns false for unknown/finished session.</summary>
    public bool AppendChunk(Guid id, ReadOnlyMemory<byte> data)
    {
        var s = Get(id);
        if (s is null || s.Status != "active" || data.Length == 0)
        {
            return false;
        }
        lock (s.Gate)
        {
            using var fs = new FileStream(s.TempPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            fs.Write(data.Span);
        }
        return true;
    }

    /// <summary>Bytes [from, to) of the session's PCM file.</summary>
    public byte[] ReadRange(LiveSession s, long from, long to)
    {
        lock (s.Gate)
        {
            using var fs = new FileStream(s.TempPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(from, SeekOrigin.Begin);
            var buf = new byte[to - from];
            var read = 0;
            while (read < buf.Length)
            {
                var n = fs.Read(buf, read, buf.Length - read);
                if (n <= 0) { break; }
                read += n;
            }
            return read == buf.Length ? buf : buf[..read];
        }
    }

    public long FileLength(LiveSession s)
    {
        lock (s.Gate)
        {
            return new FileInfo(s.TempPath).Length;
        }
    }

    public void Finish(Guid id)
    {
        var s = Get(id);
        if (s is not null && s.Status == "active")
        {
            s.Status = "finishing";
        }
    }

    public void Remove(Guid id)
    {
        if (_sessions.TryRemove(id, out var s))
        {
            try { File.Delete(s.TempPath); } catch { /* best effort */ }
        }
    }
}
