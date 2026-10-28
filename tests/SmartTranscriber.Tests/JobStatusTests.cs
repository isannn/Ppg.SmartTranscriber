using NUnit.Framework;

using SmartTranscriber.Core.Domain;

namespace SmartTranscriber.Tests;

[TestFixture]
public class JobStatusTests
{
    [TestCase("Pending", JobStatus.Pending)]
    [TestCase("processing", JobStatus.Processing)]
    [TestCase("DONE", JobStatus.Done)]
    [TestCase("failed", JobStatus.Failed)]
    public void Parse_IsCaseInsensitive(string input, JobStatus expected)
    {
        Assert.That(Enum.Parse<JobStatus>(input, ignoreCase: true), Is.EqualTo(expected));
    }

    [Test]
    public void AllStatusStrings_RoundTrip()
    {
        foreach (var status in Enum.GetValues<JobStatus>())
        {
            Assert.That(Enum.Parse<JobStatus>(status.ToString()), Is.EqualTo(status));
        }
    }
}
