using MongoDB.Driver;

namespace JobOrchestrator.Infrastructure.Persistence;

public sealed class MongoSessionAccessor : IMongoSessionAccessor
{
    private readonly AsyncLocal<IClientSessionHandle?> _current = new();

    public IClientSessionHandle? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
