using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Strive.Controllers;
using Strive.Core.Interfaces;
using Strive.Core.Services;
using Strive.Core.Services.WatchTogether;
using Strive.Core.Services.WatchTogether.Requests;
using Strive.Hubs.Core;
using Strive.Hubs.Core.Dtos;
using Strive.IntegrationTests._Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    [Collection(IntegrationTestCollection.Definition)]
    public class WatchTogetherTests : ServiceIntegrationTest
    {
        private const string Link = "https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=15";

        public WatchTogetherTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper,
            mongoDb)
        {
        }

        private static Task<SuccessOrError<Unit>> Start(UserConnection connection, string url = Link) =>
            connection.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.StartWatchTogether),
                new StartWatchTogetherDto(url));

        private static Task<SuccessOrError<Unit>> Control(UserConnection connection, WatchTogetherAction action,
            double? position = null, double? rate = null) =>
            connection.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.ControlWatchTogether),
                new ControlWatchTogetherDto(action, position, rate));

        private static Task<SuccessOrError<Unit>> Stop(UserConnection connection) =>
            connection.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.StopWatchTogether));

        [Fact]
        public async Task Start_Moderator_EverybodySeesTheVideo()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            var user = CreateUser();
            var userConnection = await ConnectUserToConference(user, conference);

            AssertSuccess(await Start(moderator));

            await userConnection.SyncObjects.AssertSyncObject<SynchronizedWatchTogether>(
                SynchronizedWatchTogether.SyncObjId, state =>
                {
                    var session = Assert.IsType<WatchTogetherSession>(state.Session);
                    Assert.Equal("youtube", session.Provider);
                    Assert.Equal("dQw4w9WgXcQ", session.VideoId);
                    Assert.Equal(moderator.User.Sub, session.StartedBy);
                    Assert.Equal(WatchTogetherPlaybackState.Playing, session.State);
                    Assert.Equal(15, session.PositionSeconds);
                });
        }

        [Fact]
        public async Task Start_ParticipantWithoutPermission_ReturnPermissionDenied()
        {
            var (_, conference) = await ConnectToOpenedConference();
            var userConnection = await ConnectUserToConference(CreateUser(), conference);

            var result = await Start(userConnection);

            AssertErrorCode(ServiceErrorCode.PermissionDenied, result.Error!);
        }

        [Fact]
        public async Task Start_NotAYouTubeLink_ReturnError_AndNothingIsShown()
        {
            var (moderator, _) = await ConnectToOpenedConference();

            var result = await Start(moderator, "https://evil.example.com/watch?v=dQw4w9WgXcQ");

            AssertErrorCode(ServiceErrorCode.WatchTogether_InvalidVideoUrl, result.Error!);
            await moderator.SyncObjects.AssertSyncObject<SynchronizedWatchTogether>(
                SynchronizedWatchTogether.SyncObjId, state => Assert.Null(state.Session));
        }

        [Fact]
        public async Task Control_PauseSeekAndRate_AreSharedWithEverybody()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            var userConnection = await ConnectUserToConference(CreateUser(), conference);
            AssertSuccess(await Start(moderator));

            AssertSuccess(await Control(moderator, WatchTogetherAction.Pause, 42.5));
            await userConnection.SyncObjects.AssertSyncObject<SynchronizedWatchTogether>(
                SynchronizedWatchTogether.SyncObjId, state =>
                {
                    Assert.Equal(WatchTogetherPlaybackState.Paused, state.Session!.State);
                    Assert.Equal(42.5, state.Session.PositionSeconds);
                });

            AssertSuccess(await Control(moderator, WatchTogetherAction.SetRate, rate: 1.25));
            await userConnection.SyncObjects.AssertSyncObject<SynchronizedWatchTogether>(
                SynchronizedWatchTogether.SyncObjId, state => Assert.Equal(1.25, state.Session!.Rate));
        }

        [Fact]
        public async Task Control_ParticipantWithoutPermission_ReturnPermissionDenied()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            var userConnection = await ConnectUserToConference(CreateUser(), conference);
            AssertSuccess(await Start(moderator));

            var result = await Control(userConnection, WatchTogetherAction.Pause, 1);

            AssertErrorCode(ServiceErrorCode.PermissionDenied, result.Error!);
        }

        [Fact]
        public async Task Control_InvalidPosition_ReturnError()
        {
            var (moderator, _) = await ConnectToOpenedConference();
            AssertSuccess(await Start(moderator));

            var result = await Control(moderator, WatchTogetherAction.Seek, -3);

            AssertErrorCode(ServiceErrorCode.WatchTogether_InvalidControl, result.Error!);
        }

        [Fact]
        public async Task Join_AfterTheVideoStarted_SeesTheVideo()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            AssertSuccess(await Start(moderator));

            var lateConnection = await ConnectUserToConference(CreateUser(), conference);

            await lateConnection.SyncObjects.AssertSyncObject<SynchronizedWatchTogether>(
                SynchronizedWatchTogether.SyncObjId, state => Assert.Equal("dQw4w9WgXcQ", state.Session!.VideoId));
        }

        [Fact]
        public async Task Stop_RemovesTheVideoForEverybody()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            var userConnection = await ConnectUserToConference(CreateUser(), conference);
            AssertSuccess(await Start(moderator));
            await userConnection.SyncObjects.WaitForSyncObj<SynchronizedWatchTogether>(
                SynchronizedWatchTogether.SyncObjId);

            AssertSuccess(await Stop(moderator));

            await userConnection.SyncObjects.AssertSyncObject<SynchronizedWatchTogether>(
                SynchronizedWatchTogether.SyncObjId, state => Assert.Null(state.Session));
        }

        [Fact]
        public async Task Time_ReturnsTheTimeOfTheServer_WithoutSignIn()
        {
            var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var response = await Client.GetFromJsonAsync<TimeResponse>("/v1/time");

            Assert.InRange(response!.UtcMilliseconds, before - 1000, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1000);
        }
    }
}
