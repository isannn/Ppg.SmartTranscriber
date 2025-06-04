using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Ppg.SmartTranscriber.Data;
using Ppg.SmartTranscriber.Interfaces.Ppg.SmartTranscriber.Interfaces;

namespace Ppg.SmartTranscriber.Services
{
    public class MinioService : IMinioService
    {
        private readonly IMinioClient _minioClient;
        private readonly MinioSettings _settings;
        private readonly ILogger<MinioService> _logger;

        public MinioService(IMinioClient minioClient, IOptions<MinioSettings> options, ILogger<MinioService> logger)
        {
            _minioClient = minioClient;
            _settings = options.Value;
            _logger = logger;
        }

        public async Task UploadFileAsync(string objectName, Stream fileStream, string contentType, string? bucketName = null)
        {
            try
            {
                var actualBucketName = bucketName ?? _settings.BucketName;
                _logger.LogInformation("Attempting to upload file {ObjectName} to bucket {BucketName}", objectName, actualBucketName);

                // Ensure bucket exists
                var found = await _minioClient.BucketExistsAsync(
                    new BucketExistsArgs().WithBucket(actualBucketName));
                if (!found)
                {
                    _logger.LogInformation("Bucket {BucketName} does not exist, creating it", actualBucketName);
                    await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(actualBucketName));
                }

                // Upload file
                var putArgs = new PutObjectArgs()
                    .WithBucket(actualBucketName)
                    .WithObject(objectName)
                    .WithStreamData(fileStream)
                    .WithObjectSize(fileStream.Length)
                    .WithContentType(contentType);

                _logger.LogInformation("Starting file upload for {ObjectName}", objectName);
                await _minioClient.PutObjectAsync(putArgs);
                _logger.LogInformation("Successfully uploaded file {ObjectName}", objectName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file {ObjectName} to Minio", objectName);
                throw;
            }
        }

        public async Task<Stream> DownloadFileAsync(string objectName, string? bucketName = null)
        {
            try
            {
                var memoryStream = new MemoryStream();
                var actualBucketName = bucketName ?? _settings.BucketName;

                _logger.LogInformation("Attempting to download file {ObjectName} from bucket {BucketName}", objectName, actualBucketName);

                await _minioClient.GetObjectAsync(new GetObjectArgs()
                    .WithBucket(actualBucketName)
                    .WithObject(objectName)
                    .WithCallbackStream(stream =>
                    {
                        stream.CopyTo(memoryStream);
                    }));

                memoryStream.Position = 0;
                _logger.LogInformation("Successfully downloaded file {ObjectName}", objectName);
                return memoryStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file {ObjectName} from Minio", objectName);
                throw;
            }
        }

        public async Task DeleteFileAsync(string objectName, string? bucketName = null)
        {
            try
            {
                var actualBucketName = bucketName ?? _settings.BucketName;
                _logger.LogInformation("Attempting to delete file {ObjectName} from bucket {BucketName}", objectName, actualBucketName);

                await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(actualBucketName)
                    .WithObject(objectName));

                _logger.LogInformation("Successfully deleted file {ObjectName}", objectName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {ObjectName} from Minio", objectName);
                throw;
            }
        }

        public async Task<bool> BucketExistsAsync(string? bucketName = null)
        {
            try
            {
                var actualBucketName = bucketName ?? _settings.BucketName;
                return await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(actualBucketName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if bucket {BucketName} exists", bucketName);
                throw;
            }
        }

        public async Task CreateBucketIfNotExistsAsync(string? bucketName = null)
        {
            try
            {
                var actualBucketName = bucketName ?? _settings.BucketName;
                var exists = await BucketExistsAsync(actualBucketName);
                if (!exists)
                {
                    _logger.LogInformation("Creating bucket {BucketName}", actualBucketName);
                    await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(actualBucketName));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating bucket {BucketName}", bucketName);
                throw;
            }
        }
    }
}
