namespace SmartTranscriber.Core.Ports;

/// <summary>Fetches remote media (YouTube &c.) to a local file.</summary>
public interface IMediaFetcher
{
    /// <summary>Downloads <paramref name="url"/> into <paramref name="workDir"/>; returns the file path.</summary>
    Task<string> FetchAsync(string url, string workDir, CancellationToken ct = default);
}
