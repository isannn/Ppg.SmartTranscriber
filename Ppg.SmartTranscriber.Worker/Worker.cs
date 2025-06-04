using System.Diagnostics;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Minio;
using Minio.DataModel.Args;
using Ppg.SmartTranscriber.Data;
using Ppg.SmartTranscriber.Interfaces;

namespace Ppg.SmartTranscriber.Worker
{
    public class TranscriptionWorker(
        IServiceProvider serviceProvider,
        ILogger<TranscriptionWorker> logger,
        IOptions<MinioSettings> minioSettings) : BackgroundService
    {
        private readonly ILogger<TranscriptionWorker> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SmartTranscriberDbContext>();
                var minio = scope.ServiceProvider.GetRequiredService<IMinioClient>();

                var job = await db.TranscriptionJobs
                    .Where(j => j.Status == "Pending")
                    .OrderBy(j => j.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken: stoppingToken);

                if (job != null)
                {
                    try
                    {
                        job.Status = "Processing";
                        await db.SaveChangesAsync(stoppingToken);

                        // Download file from MinIO
                        var audioFilePath = Path.GetTempFileName();
                        await using (var fs = File.Create(audioFilePath))
                        {
                            await minio.GetObjectAsync(new GetObjectArgs()
                                .WithBucket(minioSettings.Value.BucketName)
                                .WithObject(job.ObjectName)
                                .WithCallbackStream(s => s.CopyTo(fs)), stoppingToken);
                        }

                        // Run Whisper.cpp via CLI
                        var segments = await RunWhisperCpp(job.Id, audioFilePath); // implement this method

                        job.Status = "Done";
                        job.Segments = segments;
                        await db.SaveChangesAsync(stoppingToken);

                        File.Delete(audioFilePath);
                    }
                    catch (Exception ex)
                    {
                        job.Status = "Failed";
                        job.ErrorMessage = ex.Message;
                        await db.SaveChangesAsync(stoppingToken);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        private async Task<List<TranscriptionSegment>> RunWhisperCpp(Guid jobId, string filePath)
        {
            var outputPath = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(filePath)}.json");

            var psi = new ProcessStartInfo
            {
                FileName = "whisper", // or ./main if using whisper.cpp compiled binary
                Arguments = $"\"{filePath}\" --output_format json --output_dir \"{Path.GetTempPath()}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            await proc?.WaitForExitAsync()!;

            if (!File.Exists(outputPath))
                throw new Exception("Whisper output not found.");

            var json = await File.ReadAllTextAsync(outputPath);
            var segments = JsonSerializer.Deserialize<List<WhisperSegmentDto>>(json);

            if (segments == null)
                throw new Exception("Failed to parse Whisper output");

            return segments.Select(s => new TranscriptionSegment
            {
                JobId = jobId,
                StartTime = s.start,
                EndTime = s.end,
                Text = s.text.Trim(),
                Speaker = s.speaker
            }).ToList();
        }
    }
}
