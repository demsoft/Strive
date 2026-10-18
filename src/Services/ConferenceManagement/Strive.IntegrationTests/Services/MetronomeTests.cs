using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Strive.Core.Interfaces;
using Strive.Core.Services;
using Strive.Core.Services.Metronome;
using Strive.Hubs.Core;
using Strive.Hubs.Core.Dtos;
using Strive.IntegrationTests._Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    [Collection(IntegrationTestCollection.Definition)]
    public class MetronomeTests : ServiceIntegrationTest
    {
        public MetronomeTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper,
            mongoDb)
        {
        }

        private static Task<SuccessOrError<Unit>> Start(UserConnection connection, int bpm = 100, int beats = 4) =>
            connection.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.StartMetronome),
                new StartMetronomeDto(bpm, beats));

        private static Task<SuccessOrError<Unit>> Stop(UserConnection connection) =>
            connection.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.StopMetronome));

        [Fact]
        public async Task Start_Moderator_EverybodySeesTheTempo()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            var student = await ConnectUserToConference(CreateUser(), conference);

            AssertSuccess(await Start(moderator, 96, 3));

            await student.SyncObjects.AssertSyncObject<SynchronizedMetronome>(SynchronizedMetronome.SyncObjId,
                value =>
                {
                    Assert.Equal(96, value.Session!.Bpm);
                    Assert.Equal(3, value.Session.BeatsPerBar);
                    Assert.Equal(moderator.User.Sub, value.Session.StartedBy);
                });
        }

        [Fact]
        public async Task Start_ParticipantWithoutPermission_ReturnPermissionDenied()
        {
            var (_, conference) = await ConnectToOpenedConference();
            var student = await ConnectUserToConference(CreateUser(), conference);

            var result = await Start(student);

            AssertErrorCode(ServiceErrorCode.PermissionDenied, result.Error!);
        }

        [Fact]
        public async Task Start_TempoOutOfRange_ReturnError()
        {
            var (moderator, _) = await ConnectToOpenedConference();

            var result = await Start(moderator, 1000, 4);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task Stop_Running_NobodyHearsTheClickAnymore()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            var student = await ConnectUserToConference(CreateUser(), conference);
            AssertSuccess(await Start(moderator));
            await student.SyncObjects.AssertSyncObject<SynchronizedMetronome>(SynchronizedMetronome.SyncObjId,
                value => Assert.NotNull(value.Session));

            AssertSuccess(await Stop(moderator));

            await student.SyncObjects.AssertSyncObject<SynchronizedMetronome>(SynchronizedMetronome.SyncObjId,
                value => Assert.Null(value.Session));
        }
    }
}
