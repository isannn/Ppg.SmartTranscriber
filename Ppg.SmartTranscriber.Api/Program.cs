
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Minio;
using Ppg.SmartTranscriber.Data;

namespace Ppg.SmartTranscriber.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddMinioClient();


            // Add this in your Program.cs, before builder.Build()
            builder.Services.AddDbContext<SmartTranscriberDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));



            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }

        private static void AddMinioClient(WebApplicationBuilder builder)
        {
            builder.Services.AddSingleton<IMinioClient>(sp =>
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
        }
    }
}
