namespace SmartTranscriber.Core.Ports;

public interface IStorageService
{
    Task EnsureBucketAsync(CancellationToken ct = default);
    Task UploadAsync(string objectName, Stream data, string contentType, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string objectName, CancellationToken ct = default);
    Task<bool> ExistsAsync(string objectName, CancellationToken ct = default);
    Task DeleteAsync(string objectName, CancellationToken ct = default);
}
