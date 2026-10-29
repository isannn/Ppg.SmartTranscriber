using SmartTranscriber.Core.Domain;

namespace SmartTranscriber.Core.Ports;

public enum TranscriptFormat
{
    Txt,
    Srt,
    Vtt
}

public interface ITranscriptRenderer
{
    string Render(TranscriptionJob job, IReadOnlyList<TranscriptSegment> segments, TranscriptFormat format);
    string RenderText(TranscriptionJob job, IReadOnlyList<TranscriptSegment> segments);
}
