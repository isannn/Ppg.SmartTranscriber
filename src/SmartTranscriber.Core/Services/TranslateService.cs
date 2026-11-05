using Microsoft.Extensions.Logging;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Core.Services;

/// <summary>LLM-translates a job's transcript into one language — cached per job.</summary>
public sealed class TranslateService(
    IJobRepository jobs,
    ISummarizer summarizer,
    ITranscriptRenderer renderer,
    ILogger<TranslateService> logger)
{
    public async Task<TranscriptionJob?> TranslateAsync(Guid id, string to, bool regenerate, CancellationToken ct = default)
    {
        var job = await jobs.GetByIdAsync(id, ct);
        if (job is null)
        {
            return null;
        }
        if (!regenerate && job.Translation is not null && job.TranslationLang == to)
        {
            return job;
        }
        var prompt = $"Translate the following transcript into {to}. " +
                     "Keep speaker labels and timestamps. Output only the translated transcript.";
        var text = await summarizer.SummarizeAsync(renderer.RenderText(job, job.Segments),
            new SummaryRequest(Prompt: prompt), ct);
        job.Translation = text;
        job.TranslationLang = to;
        await jobs.UpdateAsync(job, ct);
        logger.LogInformation("Job {JobId}: translated to {Lang} ({Chars} chars)", id, to, text.Length);
        return job;
    }
}
