using Strive.Infrastructure.Redis.Tests.Redis;
using Strive.Infrastructure.Tests.KeyValue.Scripts.Base;
using Strive.IntegrationTests._Helpers;
using Xunit;

namespace Strive.IntegrationTests.Infrastructure.Redis.Scripts
{
    [Collection(RedisCollection.Name)]
    public class JoinedParticipantsRepository_RemoveParticipant_Redis :
        JoinedParticipantsRepository_RemoveParticipant_Tests
    {
        public JoinedParticipantsRepository_RemoveParticipant_Redis(RedisDbConnector connector) : base(
            KeyValueDatabaseFactory.Create(connector.CreateConnection()))
        {
        }
    }
}
