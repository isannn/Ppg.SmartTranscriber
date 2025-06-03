using Microsoft.Extensions.Options;
using Minio;
using Ppg.SmartTranscriber.Data;

namespace Ppg.SmartTranscriber.API
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddMinioClient(this IServiceCollection services)
        {
            services.AddSingleton<IMinioClient>(sp =>
            {
                var config = sp.GetRequiredService<IOptions<MinioSettings>>().Value;
                var endpoint = config.Endpoint;
                var useSsl = false;

                if (endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    useSsl = true;
                    endpoint = endpoint["https://".Length..];
                }
                else if (endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    endpoint = endpoint["http://".Length..];
                }

                var minioClient = new MinioClient()
                    .WithEndpoint(endpoint)
                    .WithCredentials(config.AccessKey, config.SecretKey);

                if (useSsl)
                {
                    minioClient = minioClient.WithSSL();
                }

                return minioClient.Build();
            });

            return services;
        }
    }
}
