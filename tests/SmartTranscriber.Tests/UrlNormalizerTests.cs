using NUnit.Framework;

using SmartTranscriber.Core.Services;

namespace SmartTranscriber.Tests;

[TestFixture]
public class UrlNormalizerTests
{
    [TestCase("https://www.youtube.com/watch?v=abc123")]
    [TestCase("https://youtu.be/abc123")]
    [TestCase("https://www.youtube.com/shorts/abc123")]
    [TestCase("https://www.youtube.com/embed/abc123")]
    [TestCase("https://www.youtube.com/live/abc123")]
    [TestCase("https://m.youtube.com/watch?v=abc123")]
    [TestCase("https://www.youtube.com/watch?v=abc123&t=42s")]
    [TestCase("https://www.youtube.com/watch?v=abc123&utm_source=tg&si=xyz#frag")]
    [TestCase("http://youtube.com/watch?v=abc123&list=PL1&index=2")]
    public void YouTube_Variants_Normalize_To_VideoId(string url)
    {
        Assert.That(UrlNormalizer.Normalize(url), Is.EqualTo("youtube.com/watch?v=abc123"));
    }

    [Test]
    public void NonYouTube_Keeps_Path_And_Sorted_Query()
    {
        Assert.That(
            UrlNormalizer.Normalize("https://Example.COM:443/a/b/?b=2&a=1&utm_x=z"),
            Is.EqualTo("example.com/a/b?a=1&b=2"));
    }

    [Test]
    public void NonDefault_Port_Is_Kept()
    {
        Assert.That(
            UrlNormalizer.Normalize("http://example.com:8080/x"),
            Is.EqualTo("example.com:8080/x"));
    }

    [Test]
    public void Bare_Param_Without_Value_Survives()
    {
        Assert.That(
            UrlNormalizer.Normalize("https://vimeo.com/123?muted&autoplay=1"),
            Is.EqualTo("vimeo.com/123?autoplay=1&muted"));
    }

    [Test]
    public void Trailing_Whitespace_And_Slashes_Are_Trimmed()
    {
        Assert.That(
            UrlNormalizer.Normalize("  https://example.com/page/  "),
            Is.EqualTo("example.com/page"));
    }
}
