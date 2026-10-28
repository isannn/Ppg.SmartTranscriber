using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Minio;
using Minio.DataModel.Args;

using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Storage;

public sealed class MinioStorageService(
    IMinioClient client,
    IOptions<SmartTranscriberOptions> options,
    ILogger<MinioStorageService> logger) : IStorageService
{
    private readonly MinioOptions _settings = options.Value.Minio;

    public async Task EnsureBucketAsync(CancellationToken ct = default)
    {
        var exists = await client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_settings.BucketName), ct);
        if (!exists)
        {
            logger.LogInformation("Creating bucket {BucketName}", _settings.BucketName);
            await client.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_settings.BucketName), ct);
        }
    }

    public async Task UploadAsync(string objectName, Stream data, string contentType, CancellationToken ct = default)
    {
        var args = new PutObjectArgs()
            .WithBucket(_settings.BucketName)
            .WithObject(objectName)
            .WithStreamData(data)
            .WithObjectSize(data.Length)
            .WithContentType(contentType);
        await client.PutObjectAsync(args, ct);
        logger.LogInformation("Uploaded {ObjectName} to bucket {BucketName}", objectName, _settings.BucketName);
    }

    public async Task<Stream> DownloadAsync(string objectName, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_settings.BucketName)
            .WithObject(objectName)
            .WithCallbackStream(s => s.CopyTo(ms)), ct);
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteAsync(string objectName, CancellationToken ct = default)
    {
        await client.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(_settings.BucketName)
            .WithObject(objectName), ct);
        logger.LogInformation("Deleted {ObjectName} from bucket {BucketName}", objectName, _settings.BucketName);
    }
}
