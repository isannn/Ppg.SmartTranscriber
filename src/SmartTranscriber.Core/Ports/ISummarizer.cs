namespace SmartTranscriber.Core.Ports;

public interface ISummarizer
{
    string Name { get; }
    Task<string> SummarizeAsync(string transcriptText, Dto.SummaryRequest? request, CancellationToken ct = default);
}

/// <summary>Optional availability probe — providers without it are always tried.</summary>
public interface IAvailableSummarizer : ISummarizer
{
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
