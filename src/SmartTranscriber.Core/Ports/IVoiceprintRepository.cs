using SmartTranscriber.Core.Domain;

namespace SmartTranscriber.Core.Ports;

/// <summary>Persistence for speaker voiceprints (cross-job recognition).</summary>
public interface IVoiceprintRepository
{
    Task<IReadOnlyList<Voiceprint>> ListAsync(CancellationToken ct = default);
    /// <summary>Insert or replace the embedding for a canonical speaker name.</summary>
    Task UpsertAsync(string name, double[] embedding, Guid? sourceJobId, CancellationToken ct = default);
}
