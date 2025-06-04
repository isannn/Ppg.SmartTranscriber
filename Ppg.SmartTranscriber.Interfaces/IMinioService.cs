namespace Ppg.SmartTranscriber.Interfaces
{
    using System.IO;
    using System.Threading.Tasks;

    namespace Ppg.SmartTranscriber.Interfaces
    {
        public interface IMinioService
        {
            Task UploadFileAsync(string objectName, Stream data, string contentType, string? bucketName = null);
            Task<Stream> DownloadFileAsync(string objectName, string? bucketName = null);
            Task DeleteFileAsync(string objectName, string? bucketName = null);
            Task<bool> BucketExistsAsync(string? bucketName = null);
            Task CreateBucketIfNotExistsAsync(string? bucketName = null);
        }
    }
}