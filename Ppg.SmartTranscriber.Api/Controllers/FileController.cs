using Microsoft.AspNetCore.Mvc;

using Ppg.SmartTranscriber.Services;

namespace Ppg.SmartTranscriber.API.Controllers
{
    [ApiController]
    [Route("api/file/[action]")]
    public class FileController(MinioUploader uploader) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> UploadAsset(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var objectName = $"{Guid.NewGuid()}_{file.FileName}";

            await using var stream = file.OpenReadStream();
            await uploader.UploadFileAsync(objectName, stream, file.ContentType);

            return Ok(new { Message = "File uploaded", ObjectName = objectName });
        }

        [HttpPost]
        public async Task<IActionResult> DownloadAsset([FromBody] DownloadAssetRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ObjectName))
                return BadRequest("ObjectName is required.");

            try
            {
                var stream = await uploader.DownloadFileAsync(request.ObjectName);
                return File(stream, "application/octet-stream", request.ObjectName);
            }
            catch (Minio.Exceptions.ObjectNotFoundException)
            {
                return NotFound("File not found.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving file: {ex.Message}");
            }
        }

    }
}