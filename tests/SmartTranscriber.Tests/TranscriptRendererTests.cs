using NUnit.Framework;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Services;

namespace SmartTranscriber.Tests;

[TestFixture]
public class TranscriptRendererTests
{
    [Test]
    public void RenderText_ProducesTimestampedBlocks()
    {
        var job = new TranscriptionJob
        {
            OriginalFileName = "meeting.mp4",
            Model = "gigaam-v3-e2e-rnnt",
            Language = "ru",
            DurationSeconds = 125.5
        };
        var segments = new List<TranscriptSegment>
        {
            new() { StartTime = 1.2, EndTime = 12.0, Speaker = "SPEAKER_00", Text = "hello there" },
            new() { StartTime = 12.5, EndTime = 40.0, Speaker = "SPEAKER_01", Text = "hi" }
        };

        var text = new TranscriptRenderer().RenderText(job, segments);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("source: meeting.mp4"));
            Assert.That(text, Does.Contain("[00:00:01 --> 00:00:12] SPEAKER_00"));
            Assert.That(text, Does.Contain("[00:00:12 --> 00:00:40] SPEAKER_01"));
            Assert.That(text, Does.Contain("hello there"));
        });
    }

    [Test]
    public void RenderText_RepeatsLastSpeakerWhenMissing()
    {
        var job = new TranscriptionJob { OriginalFileName = "a.wav" };
        var segments = new List<TranscriptSegment>
        {
            new() { StartTime = 0, EndTime = 5, Speaker = "SPEAKER_00", Text = "one" },
            new() { StartTime = 5, EndTime = 9, Speaker = null, Text = "two" }
        };

        var text = new TranscriptRenderer().RenderText(job, segments);

        Assert.That(text, Does.Contain("[00:00:05 --> 00:00:09] SPEAKER_00"));
    }
}
