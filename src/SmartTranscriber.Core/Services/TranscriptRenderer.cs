using System.Text;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Core.Services;

public sealed class TranscriptRenderer : ITranscriptRenderer
{
    public string Render(TranscriptionJob job, IReadOnlyList<TranscriptSegment> segments, TranscriptFormat format)
    {
        return format switch
        {
            TranscriptFormat.Srt => RenderSrt(segments),
            TranscriptFormat.Vtt => RenderVtt(segments),
            _ => RenderText(job, segments)
        };
    }

    /// <summary>SubRip (.srt): numbered cues, HH:MM:SS,mmm, speaker prefix.</summary>
    public string RenderSrt(IReadOnlyList<TranscriptSegment> segments)
    {
        var sb = new StringBuilder();
        var i = 1;
        foreach (var s in segments.OrderBy(x => x.StartTime))
        {
            sb.AppendLine(i.ToString());
            sb.AppendLine($"{FmtSrt(s.StartTime)} --> {FmtSrt(s.EndTime)}");
            sb.AppendLine(s.Speaker is null ? s.Text : $"{s.Speaker}: {s.Text}");
            sb.AppendLine();
            i++;
        }
        return sb.ToString();
    }

    /// <summary>WebVTT (.vtt): WEBVTT header, HH:MM:SS.mmm, <v speaker> voice tags.</summary>
    public string RenderVtt(IReadOnlyList<TranscriptSegment> segments)
    {
        var sb = new StringBuilder("WEBVTT\n\n");
        foreach (var s in segments.OrderBy(x => x.StartTime))
        {
            sb.AppendLine($"{FmtVtt(s.StartTime)} --> {FmtVtt(s.EndTime)}");
            sb.AppendLine(s.Speaker is null ? s.Text : $"<v {s.Speaker}>{s.Text}</v>");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string FmtSrt(double seconds)
    {
        return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss\,fff");
    }

    private static string FmtVtt(double seconds)
    {
        return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss\.fff");
    }

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
