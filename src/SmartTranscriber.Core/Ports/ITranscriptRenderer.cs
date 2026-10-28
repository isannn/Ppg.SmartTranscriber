using SmartTranscriber.Core.Domain;

namespace SmartTranscriber.Core.Ports;

public interface ITranscriptRenderer
{
    string RenderText(TranscriptionJob job, IReadOnlyList<TranscriptSegment> segments);
}
