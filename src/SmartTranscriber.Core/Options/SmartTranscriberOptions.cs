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
    public string WhisperCppPath { get; set; } = "";
    public string WhisperModelPath { get; set; } = "";
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

public sealed class WorkerOptions
{
    public int PollIntervalSeconds { get; set; } = 5;
    public string TempDir { get; set; } = "";
    public bool Enabled { get; set; } = true;
}
