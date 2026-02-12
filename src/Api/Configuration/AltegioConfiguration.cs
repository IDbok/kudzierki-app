using Infrastructure.Integrations.Altegio;

namespace Api.Configuration;

public static class AltegioConfiguration
{
    public static void AddAltegioConfiguration(this WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
            builder.Configuration.AddUserSecrets<Program>(optional: true);

        builder.Services
            .AddOptions<AltegioSettings>()
            .Bind(builder.Configuration.GetSection(AltegioSettings.SectionName))
            .PostConfigure(settings =>
            {
                settings.BearerToken = ResolveSecret(settings.BearerToken, "ALTEGIO_BEARER_TOKEN", "Altegio__BearerToken");
                settings.UserToken = ResolveSecret(settings.UserToken, "ALTEGIO_USER_TOKEN", "Altegio__UserToken");
            })
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.BaseUrl), "Altegio:BaseUrl must be configured.")
            .Validate(settings => settings.CompanyId > 0, "Altegio:CompanyId must be greater than zero.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.BearerToken),
                "Altegio bearer token is not configured. Set one of: user-secrets Altegio:BearerToken, env Altegio__BearerToken, or env ALTEGIO_BEARER_TOKEN.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.UserToken),
                "Altegio user token is not configured. Set one of: user-secrets Altegio:UserToken, env Altegio__UserToken, or env ALTEGIO_USER_TOKEN.")
            .ValidateOnStart();
    }

    private static string ResolveSecret(string? configuredValue, string environmentVariableName, string legacyEnvironmentVariableName)
    {
        if (!string.IsNullOrWhiteSpace(configuredValue))
            return configuredValue.Trim();

        var envValue = Environment.GetEnvironmentVariable(environmentVariableName);
        if (!string.IsNullOrWhiteSpace(envValue))
            return envValue.Trim();

        var legacyEnvValue = Environment.GetEnvironmentVariable(legacyEnvironmentVariableName);
        return string.IsNullOrWhiteSpace(legacyEnvValue) ? string.Empty : legacyEnvValue.Trim();
    }
}