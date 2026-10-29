using NUnit.Framework;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Ports;
using SmartTranscriber.Core.Services;

namespace SmartTranscriber.Tests;

[TestFixture]
public class SubtitleExportTests
{
    private static readonly List<TranscriptSegment> Segments = new()
    {
        new() { StartTime = 1.234, EndTime = 5.5, Speaker = "SPEAKER_00", Text = "hello there" },
        new() { StartTime = 6.0, EndTime = 9.876, Speaker = null, Text = "second line" }
    };

    private readonly TranscriptRenderer _renderer = new();

    [Test]
    public void Srt_HasNumberedCuesAndCommaTimestamps()
    {
        var srt = _renderer.RenderSrt(Segments);

        var lines = srt.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Multiple(() =>
        {
            Assert.That(lines[0].Trim(), Is.EqualTo("1"));
            Assert.That(srt, Does.Contain("00:00:01,234 --> 00:00:05,500"));
            Assert.That(srt, Does.Contain("SPEAKER_00: hello there"));
            Assert.That(srt, Does.Contain("00:00:06,000 --> 00:00:09,876"));
            Assert.That(srt, Does.Contain("second line"));
            Assert.That(srt, Does.Not.Contain("SPEAKER_00: second"));
        });
    }

    [Test]
    public void Vtt_HasHeaderAndVoiceTagsAndDotTimestamps()
    {
        var vtt = _renderer.RenderVtt(Segments);

        Assert.Multiple(() =>
        {
            Assert.That(vtt, Does.StartWith("WEBVTT"));
            Assert.That(vtt, Does.Contain("00:00:01.234 --> 00:00:05.500"));
            Assert.That(vtt, Does.Contain("<v SPEAKER_00>hello there</v>"));
            Assert.That(vtt, Does.Contain("second line"));
            Assert.That(vtt, Does.Not.Contain("<v>"));
        });
    }

    [Test]
    public void Render_DispatchesByFormat()
    {
        var job = new TranscriptionJob { OriginalFileName = "x.mp4" };
        Assert.Multiple(() =>
        {
            Assert.That(_renderer.Render(job, Segments, TranscriptFormat.Srt), Does.Contain("-->"));
            Assert.That(_renderer.Render(job, Segments, TranscriptFormat.Vtt), Does.StartWith("WEBVTT"));
            Assert.That(_renderer.Render(job, Segments, TranscriptFormat.Txt), Does.Contain("source: x.mp4"));
        });
    }
}
