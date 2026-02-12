namespace Infrastructure.Integrations.Altegio;

public sealed class AltegioSettings
{
    public const string SectionName = "Altegio";

    public string BaseUrl { get; set; } = "https://api.alteg.io/api/v1/";

    public string BearerToken { get; set; } = string.Empty;

    public string UserToken { get; set; } = string.Empty;

    public int CompanyId { get; set; }
}
