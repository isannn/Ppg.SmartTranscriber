using System.Net;
using System.Text;
using NUnit.Framework;
using SmartTranscriber.Infrastructure.Infisical;

namespace SmartTranscriber.Tests;

[TestFixture]
internal sealed class InfisicalSecretsClientTests
{
    private static readonly InfisicalOptions Opts = new(
        Url: "http://infisical.test",
        Token: "tok",
        ProjectId: "proj-1",
        Environment: "prod",
        SecretPath: "/smarttranscriber");

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }

    [Test]
    public async Task Fetch_parses_secrets_and_normalizes_key_separator()
    {
        var body = """
            {"secrets":[
                {"secretKey":"SMARTTRANSCRIBER__Minio__AccessKey","secretValue":"k1"},
                {"secretKey":"PLAIN_KEY","secretValue":"v2"}
            ]}
            """;
        var client = new InfisicalSecretsClient(new StubHandler(HttpStatusCode.OK, body));

        var result = await client.FetchAsync(Opts);

        Assert.That(result["SMARTTRANSCRIBER:Minio:AccessKey"], Is.EqualTo("k1"));
        Assert.That(result["PLAIN_KEY"], Is.EqualTo("v2"));
        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public void Fetch_non_2xx_throws()
    {
        var client = new InfisicalSecretsClient(
            new StubHandler(HttpStatusCode.Forbidden, "{}"));

        Assert.ThrowsAsync<InvalidOperationException>(() => client.FetchAsync(Opts));
    }

    [Test]
    public void Fetch_bad_json_throws()
    {
        var client = new InfisicalSecretsClient(
            new StubHandler(HttpStatusCode.OK, "not-json"));

        Assert.ThrowsAsync<InvalidOperationException>(() => client.FetchAsync(Opts));
    }

    [Test]
    public async Task Fetch_empty_secrets_returns_empty_dict()
    {
        var client = new InfisicalSecretsClient(
            new StubHandler(HttpStatusCode.OK, """{"secrets":[]}"""));

        var result = await client.FetchAsync(Opts);

        Assert.That(result, Is.Empty);
    }
}
