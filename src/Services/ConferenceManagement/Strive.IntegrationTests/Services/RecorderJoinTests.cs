using System;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Strive.Core.Interfaces;
using Strive.Core.Services;
using Strive.Core.Services.Recording;
using Strive.Core.Services.Recording.Gateways;
using Strive.Core.Services.Recording.Requests;
using Strive.Core.Services.ParticipantsList;
using Strive.Hubs.Core;
using Strive.Infrastructure.Recording;
using Strive.IntegrationTests._Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    [Collection(IntegrationTestCollection.Definition)]
    public class RecorderJoinTests : ServiceIntegrationTest
    {
        public RecorderJoinTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper,
            mongoDb)
        {
        }

        private FakeRecorderClient Recorder => Factory.Services.GetRequiredService<FakeRecorderClient>();

        private static Task<SuccessOrError<Unit>> Start(UserConnection connection)
        {
            return connection.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.StartRecording));
        }

        /// <summary>
        ///     The account of the recorder of a recording, with the token the server hands to the recorder service
        /// </summary>
        private UserAccount RecorderAccount(string recordingId, string conferenceId, string? tokenForConference = null)
        {
            var token = Factory.Services.GetRequiredService<IRecorderJoinTokenFactory>()
                .Create(recordingId, tokenForConference ?? conferenceId, TimeSpan.FromHours(1));
            return new UserAccount(RecorderParticipants.ParticipantId(recordingId), RecorderParticipants.DisplayName,
                false, token);
        }

        private async Task<(UserConnection moderator, Strive.Models.Response.ConferenceCreatedResponseDto conference, string recordingId)>
            StartRecording()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            AssertSuccess(await Start(moderator));
            var recordingId = Recorder.Started.Last(x => x.ConferenceId == conference.ConferenceId).RecordingId;
            return (moderator, conference, recordingId);
        }

        [Fact]
        public async Task Recorder_RecordingRunning_JoinsAndIsListedAsRecording()
        {
            // arrange
            var (moderator, conference, recordingId) = await StartRecording();
            var account = RecorderAccount(recordingId, conference.ConferenceId);

            // act
            var recorder = await ConnectUserToConference(account, conference);

            // assert: it is a participant like others, called "Recording", everybody can see that it is there
            await moderator.SyncObjects.AssertSyncObject<SynchronizedParticipants>(
                SynchronizedParticipants.SyncObjId, participants =>
                {
                    var entry = participants.Participants[account.Sub];
                    Assert.Equal(RecorderParticipants.DisplayName, entry.DisplayName);
                });
            Assert.NotNull(recorder);
        }

        [Fact]
        public async Task Recorder_LobbyEnabled_DoesNotWait()
        {
            // arrange
            var conferenceDto = await CreateConference(new Strive.Models.Request.CreateConferenceRequestDto
            {
                Configuration = new Strive.Core.Domain.Entities.ConferenceConfiguration
                {
                    Moderators = new[] {"0", Moderator.Sub}.ToList(),
                    Lobby = new Strive.Core.Services.Lobby.LobbyOptions {IsEnabled = true},
                },
                Permissions = new System.Collections.Generic.Dictionary<Strive.Core.Domain.Entities.PermissionType,
                    System.Collections.Generic.Dictionary<string, Newtonsoft.Json.Linq.JValue>>(),
            });
            var moderator = await ConnectUserToConference(Moderator, conferenceDto);
            AssertSuccess(await OpenConference(moderator));
            AssertSuccess(await Start(moderator));
            var recordingId = Recorder.Started.Last(x => x.ConferenceId == conferenceDto.ConferenceId).RecordingId;

            // act & assert: ConnectUserToConference fails if the participant did not join
            await ConnectUserToConference(RecorderAccount(recordingId, conferenceDto.ConferenceId), conferenceDto);
        }

        [Fact]
        public async Task Recorder_CannotCallAnythingExceptReceive()
        {
            // arrange
            var (_, conference, recordingId) = await StartRecording();
            var recorder = await ConnectUserToConference(RecorderAccount(recordingId, conference.ConferenceId),
                conference);

            // act & assert
            foreach (var method in new[]
                     {
                         nameof(CoreHub.StartRecording), nameof(CoreHub.StopRecording), nameof(CoreHub.RaiseHand),
                         nameof(CoreHub.CloseConference), nameof(CoreHub.AdmitAllParticipants),
                     })
                await Assert.ThrowsAsync<HubException>(() =>
                    recorder.Hub.InvokeAsync<SuccessOrError<Unit>>(method));

            // what the recording view needs is allowed
            await recorder.Hub.InvokeAsync(nameof(CoreHub.FetchPermissions), (string?) null);
        }

        [Fact]
        public async Task Recorder_HasNoPermissions()
        {
            var (_, conference, recordingId) = await StartRecording();
            var recorder = await ConnectUserToConference(RecorderAccount(recordingId, conference.ConferenceId),
                conference);

            await recorder.SyncObjects.AssertSyncObject<Strive.Core.Services.Permissions.SynchronizedParticipantPermissions>(
                Strive.Core.Services.Permissions.SynchronizedParticipantPermissions.SyncObjId(recorder.User.Sub),
                permissions =>
                {
                    Assert.NotEmpty(permissions.Permissions);
                    Assert.All(permissions.Permissions.Values, value =>
                    {
                        if (value.Type == Newtonsoft.Json.Linq.JTokenType.Boolean) Assert.False((bool) value!);
                    });
                });
        }

        [Fact]
        public async Task Recorder_TokenOfAnotherConference_IsRejected()
        {
            // arrange
            var (_, conference, recordingId) = await StartRecording();
            var account = RecorderAccount(recordingId, conference.ConferenceId, "another-conference");

            // act & assert
            await AssertConnectionRejected(account, conference);
        }

        [Fact]
        public async Task Recorder_RecordingIsOver_IsRejected()
        {
            // arrange
            var (moderator, conference, recordingId) = await StartRecording();
            AssertSuccess(await moderator.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.StopRecording)));

            // act & assert
            await AssertConnectionRejected(RecorderAccount(recordingId, conference.ConferenceId), conference);
        }

        [Fact]
        public async Task Recorder_TokenWithWrongSecret_IsRejected()
        {
            // arrange
            var (_, conference, recordingId) = await StartRecording();
            var tokenFactory = new JwtRecorderJoinTokenFactory(
                Microsoft.Extensions.Options.Options.Create(new RecorderOptions
                {
                    TokenSecret = "a-completely-different-secret-0123456789",
                }), TimeProvider.System);
            var forged = new UserAccount(RecorderParticipants.ParticipantId(recordingId), "Recording", false,
                tokenFactory.Create(recordingId, conference.ConferenceId, TimeSpan.FromHours(1)));

            // act & assert
            await AssertConnectionRejected(forged, conference);
        }

        private async Task AssertConnectionRejected(UserAccount account, Strive.Models.Response.ConferenceCreatedResponseDto conference)
        {
            var failed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                var connection = await ConnectUserToConference(account, conference, false,
                    hub => hub.On<object>(CoreHubMessages.OnConnectionError, _ => failed.TrySetResult(true)));
                try
                {
                    // joining failed if the server closes the connection or the join check fails
                    await connection.Hub.InvokeAsync(nameof(CoreHub.FetchPermissions), (string?) null);
                }
                finally
                {
                    await connection.Hub.DisposeAsync();
                }
            });
        }
    }
}
