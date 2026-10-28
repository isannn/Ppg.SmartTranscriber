using SmartTranscriber.Api.Components;
using SmartTranscriber.Infrastructure;
using SmartTranscriber.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(
    SmartTranscriber.Core.Config.JsonConfigLocator.Locate(), optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SmartTranscriber API", Version = "v1" });
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSmartTranscriberCore();
builder.Services.AddSmartTranscriberInfrastructure(builder.Configuration);

var app = builder.Build();

var connectionString = builder.Configuration.GetSection("SmartTranscriber")["ConnectionString"];
if (!string.IsNullOrWhiteSpace(connectionString))
{
    DatabaseMigrator.Migrate(connectionString);
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartTranscriber API V1");
    c.RoutePrefix = "swagger";
});

app.UseStaticFiles();
app.UseAntiforgery();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
