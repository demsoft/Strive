using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Strive.Core.Services;
using Strive.Infrastructure.KeyValue;
using Strive.Infrastructure.KeyValue.InMemory;
using Strive.Infrastructure.KeyValue.Repos;
using Xunit;

namespace Strive.Infrastructure.Tests.KeyValue.Repos
{
    public class HandRaiseRepositoryTests
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1700000000);

        private readonly Participant _participant = new("conference1", "participant1");
        private readonly HandRaiseRepository _repository;

        public HandRaiseRepositoryTests()
        {
            var database = new InMemoryKeyValueDatabase(new InMemoryKeyValueData(),
                new OptionsWrapper<KeyValueDatabaseOptions>(new KeyValueDatabaseOptions()));
            _repository = new HandRaiseRepository(database);
        }

        [Fact]
        public async Task Raise_HandNotRaised_ReturnTrueAndStoreTime()
        {
            var raised = await _repository.Raise(_participant, Now);

            Assert.True(raised);
            var all = await _repository.GetAll(_participant.ConferenceId);
            Assert.Equal(Now, Assert.Single(all).Value);
            Assert.Equal(_participant.Id, Assert.Single(all).Key);
        }

        [Fact]
        public async Task Raise_HandAlreadyRaised_ReturnFalseAndKeepFirstTime()
        {
            await _repository.Raise(_participant, Now);

            var raised = await _repository.Raise(_participant, Now.AddMinutes(5));

            Assert.False(raised);
            Assert.Equal(Now, Assert.Single(await _repository.GetAll(_participant.ConferenceId)).Value);
        }

        [Fact]
        public async Task Lower_HandRaised_ReturnTrueAndRemove()
        {
            await _repository.Raise(_participant, Now);

            Assert.True(await _repository.Lower(_participant));
            Assert.Empty(await _repository.GetAll(_participant.ConferenceId));
        }

        [Fact]
        public async Task Lower_HandNotRaised_ReturnFalse()
        {
            Assert.False(await _repository.Lower(_participant));
        }

        [Fact]
        public async Task GetAll_OtherConference_ReturnOnlyOwnParticipants()
        {
            await _repository.Raise(_participant, Now);
            await _repository.Raise(new Participant("conference2", "participant2"), Now);

            var all = await _repository.GetAll(_participant.ConferenceId);

            Assert.Equal(_participant.Id, Assert.Single(all).Key);
        }

        [Fact]
        public async Task RemoveAllDataOfConference_RemoveAllHandsOfConference()
        {
            await _repository.Raise(_participant, Now);
            await _repository.Raise(new Participant(_participant.ConferenceId, "participant2"), Now);
            await _repository.Raise(new Participant("conference2", "participant3"), Now);

            await _repository.RemoveAllDataOfConference(_participant.ConferenceId);

            Assert.Empty(await _repository.GetAll(_participant.ConferenceId));
            Assert.Single(await _repository.GetAll("conference2"));
        }
    }
}
