using System.Text;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Core.Services;

public sealed class TranscriptRenderer : ITranscriptRenderer
{
    public string RenderText(TranscriptionJob job, IReadOnlyList<TranscriptSegment> segments)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"source: {job.OriginalFileName}");
        sb.AppendLine($"model: {job.Model ?? "unknown"} | language: {job.Language ?? "?"} | duration: {FormatDuration(job.DurationSeconds)}");
        sb.AppendLine(new string('=', 70));
        sb.AppendLine();

        string? lastSpeaker = null;
        foreach (var s in segments.OrderBy(x => x.StartTime))
        {
            var speaker = s.Speaker ?? lastSpeaker ?? "";
            lastSpeaker = s.Speaker ?? lastSpeaker;
            sb.AppendLine($"[{FmtTs(s.StartTime)} --> {FmtTs(s.EndTime)}] {speaker}");
            sb.AppendLine(s.Text);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string FmtTs(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.ToString(@"hh\:mm\:ss");
    }

    private static string FormatDuration(double? seconds)
        => seconds.HasValue ? TimeSpan.FromSeconds(seconds.Value).ToString(@"hh\:mm\:ss") : "?";
}
