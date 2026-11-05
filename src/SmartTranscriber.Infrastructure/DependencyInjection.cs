using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Minio;

using Npgsql;

using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;
using SmartTranscriber.Core.Services;
using SmartTranscriber.Infrastructure.Media;
using SmartTranscriber.Infrastructure.Ocr;
using SmartTranscriber.Infrastructure.Persistence;
using SmartTranscriber.Infrastructure.Webhooks;
using SmartTranscriber.Infrastructure.Storage;
using SmartTranscriber.Infrastructure.Stt;
using SmartTranscriber.Infrastructure.Transcription;

namespace SmartTranscriber.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Core application services.
    /// </summary>
    public static IServiceCollection AddSmartTranscriberCore(this IServiceCollection services)
    {
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<TranslateService>();
        services.AddScoped<ClipService>();
        services.AddScoped<ITranscriptService, TranscriptService>();
        services.AddSingleton<ITranscriptRenderer, TranscriptRenderer>();
        services.AddSingleton<IJobProgressSink, NullJobProgressSink>();
        return services;
    }

    /// <summary>
    /// Registers persistence (Dapper/DbUp), storage (MinIO), STT clients
    /// and the transcription pipeline. Binds SmartTranscriberOptions from
    /// the "SmartTranscriber" configuration section.
    /// </summary>
    public static IServiceCollection AddSmartTranscriberInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SmartTranscriberOptions>(
            configuration.GetSection(SmartTranscriberOptions.SectionName));

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<SmartTranscriberOptions>>().Value;
            if (string.IsNullOrWhiteSpace(opts.ConnectionString))
            {
                throw new InvalidOperationException(
                    "SmartTranscriber:ConnectionString is not configured " +
                    "(env SMARTTRANSCRIBER__ConnectionString, Npgsql key=value format)");
            }
            return NpgsqlDataSource.Create(opts.ConnectionString);
        });

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<SmartTranscriberOptions>>().Value.Minio;
            var endpoint = opts.Endpoint
                .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                .TrimEnd('/');
            var builder = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(opts.AccessKey, opts.SecretKey);
            if (opts.UseSsl || opts.Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                builder = builder.WithSSL();
            }
            return builder.Build();
        });

        services.AddSmartTranscriberStt();

        services.AddSingleton<IJobQueue, JobQueue>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<ISegmentRepository, SegmentRepository>();
        services.AddScoped<IStorageService, MinioStorageService>();
        services.AddScoped<TranscriptionPipeline>();
        services.AddHostedService<TranscriptionWorker>();
        services.AddHostedService<WatchFolderWorker>();
        services.AddHostedService<RetentionWorker>();

        return services;
    }

    /// <summary>
    /// Registers the transcriber strategy chain only — used by the API
    /// host and by the CLI standalone `transcribe` command (no DB/MinIO).
    /// </summary>
    public static IServiceCollection AddSmartTranscriberStt(this IServiceCollection services)
    {
        services.AddHttpClient<SidecarTranscriber>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<SmartTranscriberOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(60, opts.Stt.TimeoutSeconds));
        });

        services.AddSingleton<ITranscriber, SidecarTranscriber>();
        services.AddHttpClient<AcpSummarizer>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<SmartTranscriberOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(30, opts.Llm.TimeoutSeconds));
        });
        services.AddHttpClient<OpenAiSummarizer>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<SmartTranscriberOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(30, opts.Llm.TimeoutSeconds));
        });
        services.AddTransient<ISummarizer>(sp => sp.GetRequiredService<AcpSummarizer>());
        services.AddTransient<ISummarizer>(sp => sp.GetRequiredService<OpenAiSummarizer>());
        services.AddSingleton<SummarizerChain>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<IMediaFetcher, YtDlpMediaFetcher>();
        services.AddSingleton<IKeyFrameExtractor, FfmpegKeyFrameExtractor>();
        services.AddSingleton<IClipExtractor, FfmpegClipExtractor>();
        services.AddSingleton<IMediaProbe, FfprobeMediaProbe>();
        services.AddSingleton<IOcr, TesseractOcr>();
        services.AddScoped<IFrameRepository, MediaFrameRepository>();
        services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
        services.AddScoped<IVoiceprintRepository, VoiceprintRepository>();
        services.AddHttpClient<IWebhookNotifier, HttpWebhookNotifier>();
        services.AddSingleton<TranscriberChain>();
        return services;
    }
}
