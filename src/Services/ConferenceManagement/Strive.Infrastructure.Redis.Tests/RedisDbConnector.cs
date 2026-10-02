using System.Threading.Tasks;
using StackExchange.Redis;
using Xunit;

namespace Strive.IntegrationTests._Helpers
{
    /// <summary>
    ///     Connection to the Redis server for tests (localhost:6379). The tests use their own database
    ///     (<see cref="TestDatabase" />), which is cleared before and after the test run. Other databases are not touched.
    ///     The Lua scripts build key names themselves, so the tests cannot be isolated by a key prefix.
    /// </summary>
    public class RedisDbConnector : IAsyncLifetime
    {
        public const int TestDatabase = 15;

        private readonly ConnectionMultiplexer _connection =
            ConnectionMultiplexer.Connect("localhost:6379,allowAdmin=true");

        public IDatabase CreateConnection()
        {
            return _connection.GetDatabase(TestDatabase);
        }

        public Task InitializeAsync()
        {
            return ClearTestDatabase();
        }

        public async Task DisposeAsync()
        {
            await ClearTestDatabase();
            await _connection.DisposeAsync();
        }

        private async Task ClearTestDatabase()
        {
            foreach (var endpoint in _connection.GetEndPoints())
            {
                await _connection.GetServer(endpoint).FlushDatabaseAsync(TestDatabase);
            }
        }
    }

    /// <summary>
    ///     All Redis tests share one database, so they must not run in parallel.
    /// </summary>
    [CollectionDefinition(Name)]
    public class RedisCollection : ICollectionFixture<RedisDbConnector>
    {
        public const string Name = "Redis";
    }
}
