using MongoDB.Driver;

namespace JobOrchestrator.Infrastructure.Persistence;

/// <summary>
/// Flows the active Mongo session across an async call graph so repositories automatically
/// join whatever transaction <see cref="MongoUnitOfWork"/> currently has open, without every
/// port method taking a session parameter. Registered as a singleton; state is per async-flow
/// via <see cref="AsyncLocal{T}"/>, not shared across requests.
/// </summary>
public interface IMongoSessionAccessor
{
    IClientSessionHandle? Current { get; set; }
}
