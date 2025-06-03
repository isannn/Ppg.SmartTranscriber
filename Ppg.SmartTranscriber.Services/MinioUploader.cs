using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Ppg.SmartTranscriber.Data;

namespace Ppg.SmartTranscriber.Services
{
    public class MinioUploader(MinioClient minioClient, IOptions<MinioSettings> options)
    {
        private readonly MinioSettings _settings = options.Value;

        public async Task UploadFileAsync(string objectName, Stream fileStream, string contentType)
        {
            // Ensure bucket exists
            bool found = await minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(_settings.BucketName));
            if (!found)
            {
                await minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(_settings.BucketName));
            }

            // Upload file
            await minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectName)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType(contentType));
        }

        public async Task<Stream> DownloadFileAsync(string objectName)
        {
            var memoryStream = new MemoryStream();

            await minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectName)
                .WithCallbackStream(stream =>
                {
                    stream.CopyTo(memoryStream);
                }));

            memoryStream.Position = 0; // reset stream position for reading
            return memoryStream;
        }

    }

}
