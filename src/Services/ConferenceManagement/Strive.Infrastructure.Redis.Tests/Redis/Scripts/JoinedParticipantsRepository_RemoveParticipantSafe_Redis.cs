using Strive.Infrastructure.Redis.Tests.Redis;
using Strive.Infrastructure.Tests.KeyValue.Scripts.Base;
using Strive.IntegrationTests._Helpers;
using Xunit;

namespace Strive.IntegrationTests.Infrastructure.Redis.Scripts
{
    [Collection(RedisCollection.Name)]
    public class JoinedParticipantsRepository_RemoveParticipantSafe_Redis :
        JoinedParticipantsRepository_RemoveParticipantSafe_Tests
    {
        public JoinedParticipantsRepository_RemoveParticipantSafe_Redis(RedisDbConnector connector) : base(
            KeyValueDatabaseFactory.Create(connector.CreateConnection()))
        {
        }
    }
}
