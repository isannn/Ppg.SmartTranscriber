using System.Net.Http.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SmartTranscriber.Core.Config;
using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;
using SmartTranscriber.Core.Services;
using SmartTranscriber.Infrastructure;
using SmartTranscriber.Infrastructure.Persistence;

namespace SmartTranscriber.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }

        try
        {
            return args[0] switch
            {
                "transcribe" => await TranscribeCommand.RunAsync(args[1..]),
                "api" => await ApiCommand.RunAsync(args[1..]),
                "db" => DbCommand.Run(args[1..]),
                _ => Unknown(args[0])
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"unknown command: {command}");
        PrintUsage();
        return 2;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            SmartTranscriber CLI

            Usage:
              smarttranscriber transcribe <file> [-o out.txt] [--model M] [--language L] [--no-diarize]
                  Transcribe a media file standalone (stt sidecar, whisper.cpp fallback).
              smarttranscriber api upload <file>
              smarttranscriber api jobs [--status Pending|Processing|Done|Failed] [--limit N]
              smarttranscriber api job <id>
              smarttranscriber api transcript <id> [-o file.txt] [--format txt|json]
              smarttranscriber api search <query> [--limit N]
              smarttranscriber api summarize <id>
                  Talk to the API service (default http://127.0.0.1:8460).
              smarttranscriber db migrate
                  Apply pending DbUp migrations.

            Config: smarttranscriber.json (repo root) + env SMARTTRANSCRIBER__*.
            """);
    }
}

file static class TranscribeCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var file = args.FirstOrDefault(a => !a.StartsWith('-'));
        var output = OptValue(args, "-o") ?? OptValue(args, "--output");
        var model = OptValue(args, "--model");
        var language = OptValue(args, "--language");
        var diarize = !args.Contains("--no-diarize");

        if (file is null || !File.Exists(file))
        {
            Console.Error.WriteLine(file is null
                ? "usage: transcribe <file> [-o out.txt] [--model M] [--language L] [--no-diarize]"
                : $"file not found: {file}");
            return 2;
        }

        var config = BuildConfig();
        await using var provider = new ServiceCollection()
            .AddLogging(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Warning))
            .Configure<SmartTranscriberOptions>(config.GetSection(SmartTranscriberOptions.SectionName))
            .AddSmartTranscriberStt()
            .AddSingleton<ITranscriptRenderer, TranscriptRenderer>()
            .BuildServiceProvider();

        var chain = provider.GetRequiredService<TranscriberChain>();
        var renderer = provider.GetRequiredService<ITranscriptRenderer>();
        var defaults = config.GetSection(SmartTranscriberOptions.SectionName).Get<SmartTranscriberOptions>()?.Stt
                       ?? new SttOptions();

        var (transcriber, result) = await chain.TranscribeAsync(
            Path.GetFullPath(file),
            new TranscribeOptions(
                model ?? defaults.DefaultModel,
                language ?? defaults.DefaultLanguage,
                diarize));

        var pseudoJob = new TranscriptionJob
        {
            OriginalFileName = Path.GetFileName(file),
            Model = result.Model ?? transcriber.Name,
            Language = result.Language,
            DurationSeconds = result.DurationSeconds
        };
        var text = renderer.RenderText(pseudoJob, result.Segments);

        var outPath = output ?? Path.ChangeExtension(file, ".transcript.txt");
        await File.WriteAllTextAsync(outPath, text);
        Console.WriteLine($"transcriber: {transcriber.Name} | segments: {result.Segments.Count}");
        Console.WriteLine($"output: {Path.GetFullPath(outPath)}");
        return 0;
    }

    internal static string? OptValue(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    internal static IConfiguration BuildConfig()
    {
        return new ConfigurationBuilder()
            .AddJsonFile(JsonConfigLocator.Locate(), optional: true)
            .AddEnvironmentVariables()
            .Build();
    }
}

file static class ApiCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: api <upload|jobs|job|transcript|search> ...");
            return 2;
        }

        var config = TranscribeCommand.BuildConfig();
        var baseUrl = config.GetSection(SmartTranscriberOptions.SectionName)["Api:BaseUrl"]
                      ?? "http://127.0.0.1:8460";
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };

        return args[0] switch
        {
            "upload" => await UploadAsync(http, args[1..]),
            "jobs" => await JobsAsync(http, args[1..]),
            "job" => await JobAsync(http, args[1..]),
            "transcript" => await TranscriptAsync(http, args[1..]),
            "search" => await SearchAsync(http, args[1..]),
            "summarize" => await SummarizeAsync(http, args[1..]),
            _ => UnknownApi(args[0])
        };
    }

    private static int UnknownApi(string sub)
    {
        Console.Error.WriteLine($"unknown api command: {sub}");
        return 2;
    }

    private static async Task<int> UploadAsync(HttpClient http, string[] args)
    {
        var file = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (file is null || !File.Exists(file))
        {
            Console.Error.WriteLine("usage: api upload <file>");
            return 2;
        }

        await using var stream = File.OpenRead(file);
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(stream), "file", Path.GetFileName(file));
        var response = await http.PostAsync("api/v1/files", content);
        Console.WriteLine(await response.Content.ReadAsStringAsync());
        return response.IsSuccessStatusCode ? 0 : 1;
    }

    private static async Task<int> JobsAsync(HttpClient http, string[] args)
    {
        var status = TranscribeCommand.OptValue(args, "--status");
        var limit = TranscribeCommand.OptValue(args, "--limit") ?? "50";
        var url = $"api/v1/jobs?limit={limit}" + (status is null ? "" : $"&status={status}");
        Console.WriteLine(await http.GetStringAsync(url));
        return 0;
    }

    private static async Task<int> JobAsync(HttpClient http, string[] args)
    {
        var id = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (id is null)
        {
            Console.Error.WriteLine("usage: api job <id>");
            return 2;
        }
        var response = await http.GetAsync($"api/v1/jobs/{id}");
        Console.WriteLine(await response.Content.ReadAsStringAsync());
        return response.IsSuccessStatusCode ? 0 : 1;
    }

    private static async Task<int> TranscriptAsync(HttpClient http, string[] args)
    {
        var id = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (id is null)
        {
            Console.Error.WriteLine("usage: api transcript <id> [-o file.txt] [--format txt|json]");
            return 2;
        }
        var format = TranscribeCommand.OptValue(args, "--format") ?? "txt";
        var output = TranscribeCommand.OptValue(args, "-o") ?? TranscribeCommand.OptValue(args, "--output");

        var text = await http.GetStringAsync($"api/v1/jobs/{id}/transcript?format={format}");
        if (output is not null)
        {
            await File.WriteAllTextAsync(output, text);
            Console.WriteLine($"output: {Path.GetFullPath(output)}");
        }
        else
        {
            Console.WriteLine(text);
        }
        return 0;
    }

    private static async Task<int> SummarizeAsync(HttpClient http, string[] args)
    {
        var id = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (id is null)
        {
            Console.Error.WriteLine("usage: api summarize <id>");
            return 2;
        }
        var response = await http.PostAsync($"api/v1/jobs/{id}/summary", null);
        Console.WriteLine(await response.Content.ReadAsStringAsync());
        return response.IsSuccessStatusCode ? 0 : 1;
    }

    private static async Task<int> SearchAsync(HttpClient http, string[] args)
    {
        var query = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (query is null)
        {
            Console.Error.WriteLine("usage: api search <query> [--limit N]");
            return 2;
        }
        var limit = TranscribeCommand.OptValue(args, "--limit") ?? "50";
        Console.WriteLine(await http.GetStringAsync($"api/v1/search?q={Uri.EscapeDataString(query)}&limit={limit}"));
        return 0;
    }
}

file static class DbCommand
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] != "migrate")
        {
            Console.Error.WriteLine("usage: db migrate");
            return 2;
        }

        var config = TranscribeCommand.BuildConfig();
        var connectionString = config.GetSection(SmartTranscriberOptions.SectionName)["ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("error: SmartTranscriber:ConnectionString is not configured " +
                                    "(env SMARTTRANSCRIBER__ConnectionString)");
            return 1;
        }

        DatabaseMigrator.Migrate(connectionString);
        Console.WriteLine("migrations applied");
        return 0;
    }
}
