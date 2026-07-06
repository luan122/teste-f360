using MongoDB.Bson;
using MongoDB.Driver;

namespace JobOrchestrator.IntegrationTests.TestSupport;

/// <summary>
/// Testcontainers' Mongo wait strategy returns once <c>mongod</c> accepts connections, which can
/// be slightly before the single-node replica set finishes electing itself primary — a real
/// window in which writes fail with <see cref="MongoNotPrimaryException"/>, made more likely when
/// several containers start concurrently under parallel test execution. This probes a real write
/// with a short retry loop so tests don't flake on that startup race.
/// </summary>
internal static class MongoReplicaSetReadiness
{
    public static async Task WaitUntilPrimaryIsWritableAsync(
        IMongoDatabase database, int maxAttempts = 20, int delayMilliseconds = 250)
    {
        var probe = database.GetCollection<BsonDocument>("__readiness_probe");

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await probe.InsertOneAsync(new BsonDocument("probedAt", DateTime.UtcNow));
                return;
            }
            catch (MongoNotPrimaryException) when (attempt < maxAttempts)
            {
                await Task.Delay(delayMilliseconds);
            }
            catch (MongoConnectionException) when (attempt < maxAttempts)
            {
                await Task.Delay(delayMilliseconds);
            }
        }
    }
}
