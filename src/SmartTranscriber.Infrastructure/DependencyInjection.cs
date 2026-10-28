using Dapper;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Minio;

using Npgsql;

using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;
using SmartTranscriber.Core.Services;
using SmartTranscriber.Infrastructure.Persistence;
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
        services.AddScoped<ITranscriptService, TranscriptService>();
        services.AddSingleton<ITranscriptRenderer, TranscriptRenderer>();
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

        SqlMapper.AddTypeHandler(new JobStatusHandler());

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
        services.AddSingleton<ITranscriber, WhisperCppTranscriber>();
        services.AddSingleton<TranscriberChain>();
        return services;
    }
}
