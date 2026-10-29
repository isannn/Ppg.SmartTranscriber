namespace SmartTranscriber.Core.Ports;

public interface ISummarizer
{
    string Name { get; }
    Task<string> SummarizeAsync(string transcriptText, CancellationToken ct = default);
}
