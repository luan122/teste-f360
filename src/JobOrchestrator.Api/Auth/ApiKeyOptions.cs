namespace JobOrchestrator.Api.Auth;

/// <summary>Configured under section "ApiKeys" — the set of keys accepted on the <c>X-Api-Key</c> header.</summary>
public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKeys";

    public List<string> Keys { get; set; } = [];
}
