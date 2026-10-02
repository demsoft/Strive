using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json.Linq;
using Strive.Core.Domain.Entities;
using Strive.Core.Interfaces;
using Strive.Core.Services;
using Strive.Core.Services.ConferenceControl;
using Strive.Core.Services.ConferenceManagement;
using Strive.Core.Services.HandRaise;
using Strive.Core.Services.Lobby;
using Strive.Core.Services.Permissions;
using Strive.Hubs.Core;
using Strive.Hubs.Core.Dtos;
using Strive.Hubs.Core.Responses;
using Strive.IntegrationTests._Helpers;
using Strive.Models.Request;
using Strive.Models.Response;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    [Collection(IntegrationTestCollection.Definition)]
    public class LobbyTests : ServiceIntegrationTest
    {
        public LobbyTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper,
            mongoDb)
        {
        }

        private class LobbyStatusListener
        {
            private readonly Channel<string> _statuses = Channel.CreateUnbounded<string>();

            public void Register(HubConnection connection)
            {
                connection.On<LobbyStatusDto>(CoreHubMessages.OnLobbyStatus,
                    dto => _statuses.Writer.TryWrite(dto.Status));
            }

            public async Task<string> Next()
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                return await _statuses.Reader.ReadAsync(cts.Token);
            }
        }

        private async Task<ConferenceCreatedResponseDto> CreateLobbyConference(bool lobbyEnabled = true)
        {
            return await CreateConference(new CreateConferenceRequestDto
            {
                Configuration = new ConferenceConfiguration
                {
                    Moderators = new[] {"0", Moderator.Sub}.ToList(),
                    Lobby = new LobbyOptions {IsEnabled = lobbyEnabled},
                },
                Permissions = new Dictionary<PermissionType, Dictionary<string, JValue>>(),
            });
        }

        private async Task<(UserConnection moderator, ConferenceCreatedResponseDto conference)> OpenLobbyConference(
            bool lobbyEnabled = true)
        {
            var conference = await CreateLobbyConference(lobbyEnabled);
            var moderator = await ConnectUserToConference(Moderator, conference);
            AssertSuccess(await OpenConference(moderator));
            return (moderator, conference);
        }

        private async Task<(UserConnection connection, LobbyStatusListener status)> ConnectWaiting(UserAccount user,
            ConferenceCreatedResponseDto conference)
        {
            var listener = new LobbyStatusListener();
            var connection = await ConnectUserToConference(user, conference, false, listener.Register);
            return (connection, listener);
        }

        private static Task<SuccessOrError<Unit>> Admit(UserConnection moderator, string participantId)
        {
            return moderator.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.AdmitParticipant),
                new LobbyParticipantDto(participantId));
        }

        private static Task<SuccessOrError<Unit>> Deny(UserConnection moderator, string participantId)
        {
            return moderator.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.DenyParticipant),
                new LobbyParticipantDto(participantId));
        }

        private static Task AssertLobbyEmpty(UserConnection moderator)
        {
            return moderator.SyncObjects.AssertSyncObject<SynchronizedLobby>(SynchronizedLobby.SyncObjId,
                lobby => Assert.Empty(lobby.Participants));
        }

        [Fact]
        public async Task Join_LobbyDisabled_JoinDirectly()
        {
            var (_, conference) = await OpenLobbyConference(false);
            var user = CreateUser();

            // ConnectUserToConference fails if the participant did not join
            await ConnectUserToConference(user, conference);
        }

        [Fact]
        public async Task Join_ModeratorWithLobbyEnabled_JoinDirectly()
        {
            var (_, conference) = await OpenLobbyConference();

            await ConnectUserToConference(Moderator, conference);
        }

        [Fact]
        public async Task Join_ConferenceNotOpen_JoinDirectly()
        {
            var conference = await CreateLobbyConference();

            await ConnectUserToConference(CreateUser(), conference);
        }

        [Fact]
        public async Task Join_LobbyEnabled_WaitAndModeratorSeesParticipant()
        {
            // arrange
            var (moderator, conference) = await OpenLobbyConference();
            var user = CreateUser(false, "Alice");

            // act
            var (_, status) = await ConnectWaiting(user, conference);

            // assert
            Assert.Equal(LobbyStatus.Waiting, await status.Next());
            await moderator.SyncObjects.AssertSyncObject<SynchronizedLobby>(SynchronizedLobby.SyncObjId, lobby =>
            {
                var waiting = Assert.Single(lobby.Participants);
                Assert.Equal(user.Sub, waiting.Key);
                Assert.Equal("Alice", waiting.Value.DisplayName);
            });
        }

        [Fact]
        public async Task Join_LobbyEnabled_WaitingConnectionCannotCallHubMethods()
        {
            // arrange
            var (_, conference) = await OpenLobbyConference();
            var (connection, status) = await ConnectWaiting(CreateUser(), conference);
            Assert.Equal(LobbyStatus.Waiting, await status.Next());

            // act & assert
            await Assert.ThrowsAsync<HubException>(() =>
                connection.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.RaiseHand)));
            await Assert.ThrowsAsync<HubException>(() =>
                connection.Hub.InvokeAsync(nameof(CoreHub.FetchPermissions), (string?) null));
        }

        [Fact]
        public async Task Admit_WaitingParticipant_JoinConference()
        {
            // arrange
            var (moderator, conference) = await OpenLobbyConference();
            var user = CreateUser();
            var (connection, status) = await ConnectWaiting(user, conference);
            Assert.Equal(LobbyStatus.Waiting, await status.Next());

            // act
            var result = await Admit(moderator, user.Sub);

            // assert
            AssertSuccess(result);
            Assert.Equal(LobbyStatus.Admitted, await status.Next());
            await EnsureClientJoinCompleted(new UserConnection(connection.Hub, conference.ConferenceId, user,
                connection.SyncObjects));
            await connection.SyncObjects.AssertSyncObject<SynchronizedHandRaises>(SynchronizedHandRaises.SyncObjId,
                hands => Assert.Empty(hands.Raised));
            await AssertLobbyEmpty(moderator);
        }

        [Fact]
        public async Task Admit_ParticipantWasAdmitted_RejoinWithoutWaiting()
        {
            // arrange
            var (moderator, conference) = await OpenLobbyConference();
            var user = CreateUser();
            var (connection, status) = await ConnectWaiting(user, conference);
            Assert.Equal(LobbyStatus.Waiting, await status.Next());
            AssertSuccess(await Admit(moderator, user.Sub));
            Assert.Equal(LobbyStatus.Admitted, await status.Next());
            await connection.Hub.DisposeAsync();

            // act & assert
            await ConnectUserToConference(user, conference);
        }

        [Fact]
        public async Task Admit_ParticipantNotWaiting_ReturnError()
        {
            var (moderator, _) = await OpenLobbyConference();

            var result = await Admit(moderator, "unknown");

            AssertFailed(result);
            AssertErrorCode(ServiceErrorCode.Lobby_ParticipantNotWaiting, result.Error!);
        }

        [Fact]
        public async Task Admit_UserWithoutPermission_ReturnPermissionDenied()
        {
            // arrange
            var (moderator, conference) = await OpenLobbyConference();
            var admitted = CreateUser();
            var (admittedConnection, admittedStatus) = await ConnectWaiting(admitted, conference);
            Assert.Equal(LobbyStatus.Waiting, await admittedStatus.Next());
            AssertSuccess(await Admit(moderator, admitted.Sub));
            Assert.Equal(LobbyStatus.Admitted, await admittedStatus.Next());

            var waiting = CreateUser();
            var (_, waitingStatus) = await ConnectWaiting(waiting, conference);
            Assert.Equal(LobbyStatus.Waiting, await waitingStatus.Next());

            // act
            var result = await Admit(admittedConnection, waiting.Sub);

            // assert
            AssertFailed(result);
            AssertErrorCode(ServiceErrorCode.PermissionDenied, result.Error!);
        }

        [Fact]
        public async Task Deny_WaitingParticipant_NotifyAndRemoveFromLobby()
        {
            // arrange
            var (moderator, conference) = await OpenLobbyConference();
            var user = CreateUser();
            var (_, status) = await ConnectWaiting(user, conference);
            Assert.Equal(LobbyStatus.Waiting, await status.Next());

            // act
            var result = await Deny(moderator, user.Sub);

            // assert
            AssertSuccess(result);
            Assert.Equal(LobbyStatus.Denied, await status.Next());
            await AssertLobbyEmpty(moderator);
        }

        [Fact]
        public async Task AdmitAll_MultipleWaiting_AdmitEveryone()
        {
            // arrange
            var (moderator, conference) = await OpenLobbyConference();
            var users = new[] {CreateUser(), CreateUser()};
            var statuses = new List<LobbyStatusListener>();
            foreach (var user in users)
            {
                var (_, status) = await ConnectWaiting(user, conference);
                Assert.Equal(LobbyStatus.Waiting, await status.Next());
                statuses.Add(status);
            }

            // act
            var result = await moderator.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.AdmitAllParticipants));

            // assert
            AssertSuccess(result);
            foreach (var status in statuses) Assert.Equal(LobbyStatus.Admitted, await status.Next());
            await AssertLobbyEmpty(moderator);
        }

        [Fact]
        public async Task Disconnect_WhileWaiting_RemoveFromLobby()
        {
            // arrange
            var (moderator, conference) = await OpenLobbyConference();
            var (connection, status) = await ConnectWaiting(CreateUser(), conference);
            Assert.Equal(LobbyStatus.Waiting, await status.Next());
            await moderator.SyncObjects.AssertSyncObject<SynchronizedLobby>(SynchronizedLobby.SyncObjId,
                lobby => Assert.Single(lobby.Participants));

            // act
            await connection.Hub.DisposeAsync();

            // assert
            await AssertLobbyEmpty(moderator);
        }

        [Fact]
        public async Task Kick_AdmittedParticipant_MustWaitAgain()
        {
            // arrange
            var (moderator, conference) = await OpenLobbyConference();
            var user = CreateUser();
            var (connection, status) = await ConnectWaiting(user, conference);
            Assert.Equal(LobbyStatus.Waiting, await status.Next());
            AssertSuccess(await Admit(moderator, user.Sub));
            Assert.Equal(LobbyStatus.Admitted, await status.Next());

            AssertSuccess(await moderator.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.KickParticipant),
                new KickParticipantRequestDto(user.Sub)));
            await connection.Hub.DisposeAsync();

            // act
            var (_, newStatus) = await ConnectWaiting(user, conference);

            // assert
            Assert.Equal(LobbyStatus.Waiting, await newStatus.Next());
        }

        [Fact]
        public async Task EnableLobby_PatchConference_UpdateSynchronizedConferenceInfo()
        {
            // arrange
            var (moderator, conference) = await OpenLobbyConference(false);
            await moderator.SyncObjects.AssertSyncObject<SynchronizedConferenceInfo>(
                SynchronizedConferenceInfo.SyncObjId, info => Assert.False(info.IsLobbyEnabled));

            // act
            var patch = new Microsoft.AspNetCore.JsonPatch.JsonPatchDocument<ConferenceData>();
            patch.Replace(x => x.Configuration.Lobby.IsEnabled, true);
            var response = await Client.PatchAsync($"/v1/conference/{conference.ConferenceId}", JsonNetContent.Create(patch));

            // assert
            response.EnsureSuccessStatusCode();
            await moderator.SyncObjects.AssertSyncObject<SynchronizedConferenceInfo>(
                SynchronizedConferenceInfo.SyncObjId, info => Assert.True(info.IsLobbyEnabled));
        }
    }
}
