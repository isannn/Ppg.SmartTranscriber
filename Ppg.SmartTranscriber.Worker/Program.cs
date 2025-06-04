using Ppg.SmartTranscriber.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<TranscriptionWorker>();

var host = builder.Build();
host.Run();