using Microsoft.Extensions.Options;

using Microsoft.AspNetCore.SignalR;

using SmartTranscriber.Api.Components;
using SmartTranscriber.Api.Hubs;
using SmartTranscriber.Api.Services;
using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;
using SmartTranscriber.Infrastructure;
using SmartTranscriber.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(
    SmartTranscriber.Core.Config.JsonConfigLocator.Locate(), optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
    o.MultipartBodyLengthLimit = 4L * 1024 * 1024 * 1024);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SmartTranscriber API", Version = "v1" });
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSmartTranscriberCore();
builder.Services.AddSmartTranscriberInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
// overrides the Null sink registered by AddSmartTranscriberCore
builder.Services.AddSingleton<IJobProgressSink, SignalRJobProgressSink>();

var app = builder.Build();

var connectionString = builder.Configuration.GetSection("SmartTranscriber")["ConnectionString"];
if (!string.IsNullOrWhiteSpace(connectionString))
{
    // Postgres may still be starting (e.g. docker restart) — retry instead of crashing
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            DatabaseMigrator.Migrate(connectionString);
            break;
        }
        catch (Exception ex) when (attempt < 30)
        {
            Console.Error.WriteLine($"DB migration attempt {attempt} failed: {ex.Message}; retrying in 2s");
            Thread.Sleep(2000);
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartTranscriber API V1");
    c.RoutePrefix = "swagger";
});

app.UseStaticFiles();
app.UseAntiforgery();

// bearer auth on /api/v1/* — /health stays open; media accepts ?token=
// because <audio>/<video> tags can't send Authorization headers.
app.Use(async (ctx, next) =>
{
    var expected = ctx.RequestServices
        .GetRequiredService<IOptions<SmartTranscriberOptions>>().Value.Auth.Token;
    if (!string.IsNullOrEmpty(expected)
        && ctx.Request.Path.StartsWithSegments("/api/v1")
        && !ctx.Request.Path.StartsWithSegments("/api/v1/health")
        && !ctx.Request.Path.StartsWithSegments("/api/v1/openapi.json"))
    {
        var bearer = ctx.Request.Headers.Authorization.ToString();
        var token = bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? bearer[7..].Trim()
            : ctx.Request.Query["token"].ToString();
        if (token != expected)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { error = "unauthorized" });
            return;
        }
    }
    await next();
});

app.MapControllers();
app.MapHub<ProgressHub>("/hubs/progress");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
