using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Strive.Core.Services;
using Strive.Core.Services.Lobby;
using Strive.Infrastructure.KeyValue;
using Strive.Infrastructure.KeyValue.InMemory;
using Strive.Infrastructure.KeyValue.Repos;
using Xunit;

namespace Strive.Infrastructure.Tests.KeyValue.Repos
{
    public class LobbyRepositoryTests
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1700000000);

        private readonly Participant _participant = new("conference1", "participant1");
        private readonly LobbyEntry _entry = new("connection1", "Alice", Now);
        private readonly LobbyRepository _repository;

        public LobbyRepositoryTests()
        {
            var database = new InMemoryKeyValueDatabase(new InMemoryKeyValueData(),
                new OptionsWrapper<KeyValueDatabaseOptions>(new KeyValueDatabaseOptions()));
            _repository = new LobbyRepository(database);
        }

        [Fact]
        public async Task Add_StoreEntry()
        {
            await _repository.Add(_participant, _entry);

            var all = await _repository.GetAll(_participant.ConferenceId);
            var stored = Assert.Single(all);
            Assert.Equal(_participant.Id, stored.Key);
            Assert.Equal(_entry, stored.Value);
        }

        [Fact]
        public async Task Add_OtherConference_NotReturned()
        {
            await _repository.Add(_participant, _entry);

            Assert.Empty(await _repository.GetAll("other"));
        }

        [Fact]
        public async Task TryRemove_Waiting_ReturnEntryAndRemove()
        {
            await _repository.Add(_participant, _entry);

            var removed = await _repository.TryRemove(_participant);

            Assert.Equal(_entry, removed);
            Assert.Empty(await _repository.GetAll(_participant.ConferenceId));
        }

        [Fact]
        public async Task TryRemove_NotWaiting_ReturnNull()
        {
            Assert.Null(await _repository.TryRemove(_participant));
        }

        [Fact]
        public async Task TryRemove_RemovedTwice_OnlyFirstWins()
        {
            await _repository.Add(_participant, _entry);

            Assert.NotNull(await _repository.TryRemove(_participant));
            Assert.Null(await _repository.TryRemove(_participant));
        }

        [Fact]
        public async Task TryRemove_ExpectedConnectionMatches_Remove()
        {
            await _repository.Add(_participant, _entry);

            Assert.NotNull(await _repository.TryRemove(_participant, "connection1"));
        }

        [Fact]
        public async Task TryRemove_ParticipantReplacedByNewerConnection_KeepEntry()
        {
            await _repository.Add(_participant, _entry with {ConnectionId = "connection2"});

            var removed = await _repository.TryRemove(_participant, "connection1");

            Assert.Null(removed);
            Assert.Single(await _repository.GetAll(_participant.ConferenceId));
        }

        [Fact]
        public async Task MarkAdmitted_IsAdmitted()
        {
            Assert.False(await _repository.IsAdmitted(_participant));

            await _repository.MarkAdmitted(_participant);

            Assert.True(await _repository.IsAdmitted(_participant));
            Assert.False(await _repository.IsAdmitted(new Participant("conference1", "other")));
        }

        [Fact]
        public async Task RemoveAdmitted_NotAdmittedAnymore()
        {
            await _repository.MarkAdmitted(_participant);

            await _repository.RemoveAdmitted(_participant);

            Assert.False(await _repository.IsAdmitted(_participant));
        }

        [Fact]
        public async Task RemoveAllDataOfConference_RemoveWaitingAndAdmitted()
        {
            await _repository.Add(_participant, _entry);
            await _repository.MarkAdmitted(new Participant("conference1", "other"));

            await _repository.RemoveAllDataOfConference("conference1");

            Assert.Empty(await _repository.GetAll("conference1"));
            Assert.False(await _repository.IsAdmitted(new Participant("conference1", "other")));
        }
    }
}
