namespace SmartTranscriber.Infrastructure.Infisical;

public sealed record InfisicalOptions(
    string Url,
    string Token,
    string ProjectId,
    string Environment,
    string SecretPath)
{
    public static InfisicalOptions? FromEnvironment()
    {
        var token = System.Environment.GetEnvironmentVariable("INFISICAL_TOKEN");
        var projectId = System.Environment.GetEnvironmentVariable("INFISICAL_PROJECT_ID");
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(projectId))
        {
            return null;
        }

        return new InfisicalOptions(
            Url: System.Environment.GetEnvironmentVariable("INFISICAL_URL")?.TrimEnd('/')
                 ?? "http://127.0.0.1:8464",
            Token: token,
            ProjectId: projectId,
            Environment: System.Environment.GetEnvironmentVariable("INFISICAL_ENV") ?? "prod",
            SecretPath: System.Environment.GetEnvironmentVariable("INFISICAL_SECRET_PATH")
                        ?? "/smarttranscriber");
    }
}
