namespace JobOrchestrator.Api.Auth;

/// <summary>Configured under section "Jwt". <see cref="SigningKey"/> is a symmetric secret.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
}
