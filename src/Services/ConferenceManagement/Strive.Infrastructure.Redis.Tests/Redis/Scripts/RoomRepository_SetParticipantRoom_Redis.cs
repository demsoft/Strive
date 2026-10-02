using Strive.Infrastructure.Redis.Tests.Redis;
using Strive.Infrastructure.Tests.KeyValue.Scripts.Base;
using Strive.IntegrationTests._Helpers;
using Xunit;

namespace Strive.IntegrationTests.Infrastructure.Redis.Scripts
{
    [Collection(RedisCollection.Name)]
    public class RoomRepository_SetParticipantRoom_Redis : RoomRepository_SetParticipantRoom_Tests
    {
        public RoomRepository_SetParticipantRoom_Redis(RedisDbConnector connector) : base(
            KeyValueDatabaseFactory.Create(connector.CreateConnection()))
        {
        }
    }
}
