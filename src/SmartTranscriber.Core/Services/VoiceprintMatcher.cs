using SmartTranscriber.Core.Domain;

namespace SmartTranscriber.Core.Services;

/// <summary>Cosine-similarity matching of speaker embeddings against stored voiceprints.</summary>
public static class VoiceprintMatcher
{
    /// <summary>Best matching voiceprint at or above threshold, or null.</summary>
    public static Voiceprint? BestMatch(double[] embedding, IReadOnlyList<Voiceprint> known, double threshold)
    {
        Voiceprint? best = null;
        var bestSim = threshold;
        foreach (var vp in known)
        {
            if (vp.Embedding.Length == 0 || vp.Embedding.Length != embedding.Length)
            {
                continue;
            }
            var sim = Cosine(embedding, vp.Embedding);
            if (sim >= bestSim)
            {
                bestSim = sim;
                best = vp;
            }
        }
        return best;
    }

    private static double Cosine(double[] a, double[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return na > 0 && nb > 0 ? dot / (Math.Sqrt(na) * Math.Sqrt(nb)) : 0;
    }
}
