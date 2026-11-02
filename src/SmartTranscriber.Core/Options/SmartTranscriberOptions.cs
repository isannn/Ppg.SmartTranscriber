namespace SmartTranscriber.Core.Options;

public sealed class SmartTranscriberOptions
{
    public const string SectionName = "SmartTranscriber";

    /// <summary>
    /// PostgreSQL connection string (Npgsql key=value format).
    /// Secret — configure via env SMARTTRANSCRIBER__ConnectionString.
    /// </summary>
    public string ConnectionString { get; set; } = "";

    public ApiOptions Api { get; set; } = new();
    public MinioOptions Minio { get; set; } = new();
    public SttOptions Stt { get; set; } = new();
    public WorkerOptions Worker { get; set; } = new();
    public LlmOptions Llm { get; set; } = new();
    public YtDlpOptions YtDlp { get; set; } = new();
    public WebhookOptions Webhooks { get; set; } = new();
    public ConsumerOptions Consumers { get; set; } = new();
    public AuthOptions Auth { get; set; } = new();
    public KeyFrameOptions KeyFrames { get; set; } = new();
    public SummaryOptions Summary { get; set; } = new();
    public WatchFolderOptions WatchFolder { get; set; } = new();
    public RetentionOptions Retention { get; set; } = new();
}

public sealed class ApiOptions
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:8460";
}

public sealed class MinioOptions
{
    public string Endpoint { get; set; } = "http://127.0.0.1:8463";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string BucketName { get; set; } = "transcriptions";
    public bool UseSsl { get; set; }
}

public sealed class SttOptions
{
    public string SidecarUrl { get; set; } = "http://127.0.0.1:8461";
    public string DefaultModel { get; set; } = "gigaam-v3-e2e-rnnt";
    public string DefaultLanguage { get; set; } = "ru";
    public bool Diarize { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 3600;
}

public sealed class LlmOptions
{
    /// <summary>Primary: tools/llm_bridge (devin acp) loopback URL.</summary>
    public string BridgeUrl { get; set; } = "http://127.0.0.1:8464";

    /// <summary>Fallback: OpenAI-compatible endpoint (OpenRouter free models).</summary>
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string Model { get; set; } = "deepseek/deepseek-chat-v3-0324:free";
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 600;
    public int MaxTranscriptChars { get; set; } = 120000;
}

public sealed class YtDlpOptions
{
    public string Command { get; set; } = "py";
    public string Args { get; set; } = "-3.11 -m yt_dlp";
    /// <summary>SOCKS proxy; falls back to the TG_PROXY env var when empty.</summary>
    public string Proxy { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 600;
}

public sealed class RetentionOptions
{
    /// <summary>Delete Done/Failed jobs older than this. 0 = disabled.</summary>
    public int MaxAgeDays { get; set; } = 0;
    public int SweepMinutes { get; set; } = 60;
}

public sealed class WatchFolderOptions
{
    /// <summary>Inbox directory — new media files here become jobs. Empty = disabled.</summary>
    public string Dir { get; set; } = "";
    public int PollSeconds { get; set; } = 30;
    /// <summary>Subdir under Dir processed files move into (empty = leave in place).</summary>
    public string DoneDir { get; set; } = "done";
}

public sealed class SummaryOptions
{
    /// <summary>Generate a summary automatically when a job reaches Done.</summary>
    public bool AutoGenerate { get; set; } = false;
}

public sealed class KeyFrameOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxFrames { get; set; } = 20;
    public double SceneThreshold { get; set; } = 0.35;
    /// <summary>Interval sampling step (sec) when scene-detect finds nothing.</summary>
    public int FallbackIntervalSec { get; set; } = 15;
}

public sealed class AuthOptions
{
    /// <summary>Bearer token for /api/v1/* (except /health). Empty = auth disabled.</summary>
    public string Token { get; set; } = "";
}

public sealed class WebhookOptions
{
    /// <summary>HTTP endpoints notified when a job reaches Done/Failed.</summary>
    public List<string> Endpoints { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 10;
}

public sealed class ConsumerOptions
{
    /// <summary>
    /// InterviewGrocker records root — when set, a finished job exports
    /// its transcript as `<OriginalFileName>.txt` into this folder
    /// (IG RecordsScanner walks it for *.txt).
    /// </summary>
    public string IgExportDir { get; set; } = "";
}

public sealed class WorkerOptions
{
    public int PollIntervalSeconds { get; set; } = 5;
    public string TempDir { get; set; } = "";
    public bool Enabled { get; set; } = true;
    /// <summary>Parallel drain consumers — how many jobs run concurrently.</summary>
    public int Concurrency { get; set; } = 1;
}
