namespace SmartTranscriber.Core.Config;

/// <summary>
/// Locates smarttranscriber.json by walking up from the app base
/// directory — the file lives at the repository root and is shared by
/// the Api host and the Cli.
/// </summary>
public static class JsonConfigLocator
{
    public const string FileName = "smarttranscriber.json";

    public static string Locate(string fileName = FileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        return fileName;
    }
}
