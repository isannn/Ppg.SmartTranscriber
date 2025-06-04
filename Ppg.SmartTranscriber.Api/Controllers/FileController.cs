using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Ppg.SmartTranscriber.Data;
using Ppg.SmartTranscriber.Services;

using System;
using System.Security.AccessControl;
using Ppg.SmartTranscriber.Interfaces.Ppg.SmartTranscriber.Interfaces;

namespace Ppg.SmartTranscriber.API.Controllers
{
    [ApiController]
    [Route("api/file/[action]")]
    public class FileController : ControllerBase
    {
        private readonly IMinioService _service;
        private readonly SmartTranscriberDbContext _db;
        private readonly ILogger<FileController> _logger;

        public FileController(IMinioService service, SmartTranscriberDbContext db, ILogger<FileController> logger)
        {
            _service = service;
            _db = db;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> UploadAsset(IFormFile? file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning("No file was uploaded or file was empty");
                    return BadRequest("No file uploaded.");
                }

                _logger.LogInformation("Processing file upload: {FileName}, Size: {FileSize} bytes", file.FileName, file.Length);

                var tempAssetName = $"{Guid.NewGuid()}_{file.FileName}";
                _logger.LogInformation("Generated asset name: {AssetName}", tempAssetName);

                await using var stream = file.OpenReadStream();
                await _service.UploadFileAsync(tempAssetName, stream, file.ContentType);

                var job = new TranscriptionJob
                {
                    ObjectName = tempAssetName,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };
                _db.TranscriptionJobs.Add(job);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Successfully uploaded file and created transcription job. JobId: {JobId}", job.Id);

                return Ok(new
                {
                    Message = "File uploaded and job enqueued",
                    ObjectName = tempAssetName,
                    JobId = job.Id,
                    JobStatus = job.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file upload: {FileName}", file?.FileName);
                return StatusCode(500, $"Error processing file: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> DownloadAsset([FromBody] DownloadAssetRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ObjectName))
                {
                    _logger.LogWarning("Download request received with empty ObjectName");
                    return BadRequest("ObjectName is required.");
                }

                _logger.LogInformation("Processing download request for object: {ObjectName}", request.ObjectName);

                var stream = await _service.DownloadFileAsync(request.ObjectName);
                _logger.LogInformation("Successfully retrieved file: {ObjectName}", request.ObjectName);

                return File(stream, "application/octet-stream", request.ObjectName);
            }
            catch (Minio.Exceptions.ObjectNotFoundException)
            {
                _logger.LogWarning("File not found: {ObjectName}", request.ObjectName);
                return NotFound("File not found.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving file: {ObjectName}", request.ObjectName);
                return StatusCode(500, $"Error retrieving file: {ex.Message}");
            }
        }
    }
}