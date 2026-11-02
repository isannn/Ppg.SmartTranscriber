namespace SmartTranscriber.Core;

public static class MediaTypes
{
    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".m4a"] = "audio/mp4",
        [".aac"] = "audio/aac",
        [".ogg"] = "audio/ogg",
        [".opus"] = "audio/ogg",
        [".flac"] = "audio/flac",
        [".wma"] = "audio/x-ms-wma",
        [".mp4"] = "video/mp4",
        [".webm"] = "video/webm",
        [".mkv"] = "video/x-matroska",
        [".avi"] = "video/x-msvideo",
        [".mov"] = "video/quicktime"
    };

    public static string For(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return ByExtension.TryGetValue(ext, out var type) ? type : "application/octet-stream";
    }

    public static bool IsVideo(string fileName)
    {
        return For(fileName).StartsWith("video/", StringComparison.Ordinal);
    }
}
