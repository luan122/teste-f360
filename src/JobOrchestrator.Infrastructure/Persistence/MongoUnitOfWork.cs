using JobOrchestrator.Application.Abstractions;
using MongoDB.Driver;

namespace JobOrchestrator.Infrastructure.Persistence;

/// <summary>Opens a MongoDB session transaction and publishes it via <see cref="IMongoSessionAccessor"/> so repository calls inside the action automatically join it. Requires a replica set.</summary>
public sealed class MongoUnitOfWork(IMongoClient client, IMongoSessionAccessor sessionAccessor) : IUnitOfWork
{
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);
        sessionAccessor.Current = session;

        try
        {
            await session.WithTransactionAsync(
                async (_, ct) =>
                {
                    await action(ct);
                    return true;
                },
                cancellationToken: cancellationToken);
        }
        finally
        {
            sessionAccessor.Current = null;
        }
    }
}
