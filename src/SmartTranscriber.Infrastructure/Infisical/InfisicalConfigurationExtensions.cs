using Microsoft.Extensions.Configuration;

namespace SmartTranscriber.Infrastructure.Infisical;

public static class InfisicalConfigurationExtensions
{
    /// <summary>
    /// Fetches secrets from Infisical and adds them to the configuration,
    /// taking precedence over environment variables. Returns the number of
    /// keys loaded, or <c>null</c> when INFISICAL_TOKEN/INFISICAL_PROJECT_ID
    /// are not set. Throws when Infisical is configured but unreachable
    /// (fail-fast).
    /// </summary>
    public static async Task<int?> AddInfisicalSecretsAsync(
        this IConfigurationBuilder builder, CancellationToken ct = default)
    {
        var options = InfisicalOptions.FromEnvironment();
        if (options is null)
        {
            return null;
        }

        var secrets = await new InfisicalSecretsClient().FetchAsync(options, ct).ConfigureAwait(false);
        builder.AddInMemoryCollection(secrets);
        return secrets.Count;
    }
}
