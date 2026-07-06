namespace JobOrchestrator.Infrastructure.Persistence;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "joborchestrator";
}
