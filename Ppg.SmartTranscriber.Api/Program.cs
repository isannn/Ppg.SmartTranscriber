using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Minio;

using Ppg.SmartTranscriber.API;
using Ppg.SmartTranscriber.Data;
using Ppg.SmartTranscriber.Interfaces;
using Ppg.SmartTranscriber.Interfaces.Ppg.SmartTranscriber.Interfaces;
using Ppg.SmartTranscriber.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers(options =>
{
    options.SuppressInputFormatterBuffering = true;
});

// Configure request size limits
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1024L * 1024L * 1024L; // 1GB
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure request size limits
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 1024 * 1024 * 1024; // 1GB
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024 * 1024; // 1GB
});

// Configure Minio settings
builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection("Minio"));

// Register Minio client
builder.Services.AddMinioClient();

// Register Minio service
builder.Services.AddSingleton<IMinioService, MinioService>();

// Register DbContext for PostgreSQL
builder.Services.AddDbContext<SmartTranscriberDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("postgres")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Smart Transcriber API V1");
        c.RoutePrefix = "swagger";
    });
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
app.MapControllers();
app.Run();
