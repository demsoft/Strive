using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Strive.Core.Interfaces;
using Strive.Core.Services;
using Strive.Core.Services.ConferenceControl;
using Strive.Core.Services.Recording;
using Strive.Core.Services.Recording.Requests;
using Strive.Hubs.Core;
using Strive.Infrastructure.Recording;
using Strive.Infrastructure.Serialization;
using Strive.IntegrationTests._Helpers;
using Strive.Models.Request;
using Strive.Models.Response;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    [Collection(IntegrationTestCollection.Definition)]
    public class RecordingTests : ServiceIntegrationTest
    {
        private const string RecorderSecret = "integration-recorder-secret";

        public RecordingTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper,
            mongoDb)
        {
        }

        private FakeRecorderClient Recorder => Factory.Services.GetRequiredService<FakeRecorderClient>();

        private FakeRecordingStorage Storage => Factory.Services.GetRequiredService<FakeRecordingStorage>();

        private static Task<SuccessOrError<Unit>> Start(UserConnection connection)
        {
            return connection.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.StartRecording));
        }

        private static Task<SuccessOrError<Unit>> Stop(UserConnection connection)
        {
            return connection.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.StopRecording));
        }

        private async Task<T> Read<T>(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync(), JsonConfig.Default)!;
        }

        private HttpClient CreateClient(UserAccount? user = null)
        {
            var client = Factory.CreateClient();
            if (user != null)
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, user.Token);
            return client;
        }

        private async Task<RecordingDto[]> GetRecordings(UserAccount user, string conferenceId)
        {
            return await Read<RecordingDto[]>(await CreateClient(user)
                .GetAsync($"/v1/conference/{conferenceId}/recordings"));
        }

        private async Task<HttpResponseMessage> Report(string recordingId, RecorderReport report,
            string secret = RecorderSecret)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Add(RecorderOptions.SecretHeader, secret);
            return await client.PostAsync($"/internal/recorder/{recordingId}/report", new JsonNetContent(report));
        }

        private async Task<(RecordingDto recording, UserConnection moderator, string conferenceId)> RecordUntilReady(
            RecordingVisibility? visibility = null)
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            AssertSuccess(await Start(moderator));
            var recordingId = Recorder.Started.Last(x => x.ConferenceId == conference.ConferenceId).RecordingId;

            await Report(recordingId, new RecorderReport(RecorderEvent.Started));
            AssertSuccess(await Stop(moderator));
            (await Report(recordingId, new RecorderReport(RecorderEvent.Finished)
            {
                StorageKey = $"recordings/{conference.ConferenceId}/{recordingId}.mp4", SizeBytes = 2048,
                DurationSeconds = 12.5,
            })).EnsureSuccessStatusCode();

            var recording = (await GetRecordings(Moderator, conference.ConferenceId)).Single();
            if (visibility != null)
            {
                (await CreateClient(Moderator).PatchAsync($"/v1/recordings/{recording.RecordingId}/visibility",
                    new JsonNetContent(new SetRecordingVisibilityDto(visibility.Value)))).EnsureSuccessStatusCode();
                recording = (await GetRecordings(Moderator, conference.ConferenceId)).Single();
            }

            return (recording, moderator, conference.ConferenceId);
        }

        [Fact]
        public async Task StartRecording_Moderator_StartRecorderAndEveryoneSeesIt()
        {
            // arrange
            var (moderator, conference) = await ConnectToOpenedConference();
            var user = CreateUser();
            var userConnection = await ConnectUserToConference(user, conference);

            // act
            var result = await Start(moderator);

            // assert
            AssertSuccess(result);
            var command = Recorder.Started.Last(x => x.ConferenceId == conference.ConferenceId);
            Assert.Equal($"recordings/{conference.ConferenceId}/{command.RecordingId}.mp4", command.StorageKey);
            Assert.False(string.IsNullOrEmpty(command.JoinToken));

            // every participant, not only moderators, must know that the conference is recorded
            foreach (var connection in new[] {moderator, userConnection})
                await connection.SyncObjects.AssertSyncObject<SynchronizedRecording>(SynchronizedRecording.SyncObjId,
                    recording =>
                    {
                        Assert.NotNull(recording.Active);
                        Assert.Equal(command.RecordingId, recording.Active!.RecordingId);
                        Assert.Equal(RecordingStatus.Starting, recording.Active.Status);
                        Assert.Equal(moderator.User.Sub, recording.Active.StartedBy);
                    });
        }

        [Fact]
        public async Task StartRecording_ParticipantWithoutPermission_ReturnPermissionDenied()
        {
            var (_, conference) = await ConnectToOpenedConference();
            var userConnection = await ConnectUserToConference(CreateUser(), conference);

            var result = await Start(userConnection);

            AssertFailed(result);
            AssertErrorCode(ServiceErrorCode.PermissionDenied, result.Error!);
            Assert.DoesNotContain(Recorder.Started, x => x.ConferenceId == conference.ConferenceId);
        }

        [Fact]
        public async Task StartRecording_ConferenceNotOpen_ReturnError()
        {
            var conference = await CreateConference(Moderator);
            var moderator = await ConnectUserToConference(Moderator, conference);

            var result = await Start(moderator);

            AssertFailed(result);
            Assert.DoesNotContain(Recorder.Started, x => x.ConferenceId == conference.ConferenceId);
        }

        [Fact]
        public async Task StartRecording_AlreadyRecording_ReturnError()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            AssertSuccess(await Start(moderator));

            var result = await Start(moderator);

            AssertFailed(result);
            AssertErrorCode(ServiceErrorCode.Recording_AlreadyRecording, result.Error!);
            Assert.Single(Recorder.Started, x => x.ConferenceId == conference.ConferenceId);
        }

        [Fact]
        public async Task StartRecording_RecorderDown_ReturnErrorAndNothingKeepsRecording()
        {
            // arrange
            var (moderator, conference) = await ConnectToOpenedConference();
            Recorder.FailOnStart = true;

            try
            {
                // act
                var result = await Start(moderator);

                // assert
                AssertFailed(result);
                AssertErrorCode(ServiceErrorCode.Recording_StartFailed, result.Error!);
                await moderator.SyncObjects.AssertSyncObject<SynchronizedRecording>(SynchronizedRecording.SyncObjId,
                    recording => Assert.Null(recording.Active));

                var failed = (await GetRecordings(Moderator, conference.ConferenceId)).Single();
                Assert.Equal(RecordingStatus.Failed, failed.Status);

                // a failed attempt does not block the next one
                Recorder.FailOnStart = false;
                AssertSuccess(await Start(moderator));
            }
            finally
            {
                Recorder.FailOnStart = false;
            }
        }

        [Fact]
        public async Task StartRecording_DisabledInConference_ReturnError()
        {
            // arrange
            var (moderator, conference) = await ConnectToOpenedConference();
            var patch = new Microsoft.AspNetCore.JsonPatch.JsonPatchDocument<Strive.Core.Services.ConferenceManagement.ConferenceData>();
            patch.Replace(x => x.Configuration.Recording.IsEnabled, false);
            (await CreateClient(Moderator).PatchAsync($"/v1/conference/{conference.ConferenceId}",
                JsonNetContent.Create(patch))).EnsureSuccessStatusCode();

            // act
            var result = await Start(moderator);

            // assert
            AssertFailed(result);
            AssertErrorCode(ServiceErrorCode.Recording_NotEnabled, result.Error!);
            await moderator.SyncObjects.AssertSyncObject<SynchronizedConferenceInfo>(
                SynchronizedConferenceInfo.SyncObjId, info => Assert.False(info.IsRecordingEnabled));
        }

        [Fact]
        public async Task ConferenceInfo_RecordingAvailable_IsRecordingEnabled()
        {
            var (moderator, _) = await ConnectToOpenedConference();

            await moderator.SyncObjects.AssertSyncObject<SynchronizedConferenceInfo>(
                SynchronizedConferenceInfo.SyncObjId, info => Assert.True(info.IsRecordingEnabled));
        }

        [Fact]
        public async Task StopRecording_Recording_TellRecorderAndFinalize()
        {
            // arrange
            var (moderator, conference) = await ConnectToOpenedConference();
            AssertSuccess(await Start(moderator));
            var recordingId = Recorder.Started.Last(x => x.ConferenceId == conference.ConferenceId).RecordingId;

            // act
            var result = await Stop(moderator);

            // assert
            AssertSuccess(result);
            Assert.Contains(recordingId, Recorder.Stopped);
            await moderator.SyncObjects.AssertSyncObject<SynchronizedRecording>(SynchronizedRecording.SyncObjId,
                recording => Assert.Equal(RecordingStatus.Finalizing, recording.Active!.Status));
        }

        [Fact]
        public async Task StopRecording_NothingRecorded_ReturnError()
        {
            var (moderator, _) = await ConnectToOpenedConference();

            var result = await Stop(moderator);

            AssertFailed(result);
            AssertErrorCode(ServiceErrorCode.Recording_NotRecording, result.Error!);
        }

        [Fact]
        public async Task CloseConference_WhileRecording_StopRecording()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            AssertSuccess(await Start(moderator));
            var recordingId = Recorder.Started.Last(x => x.ConferenceId == conference.ConferenceId).RecordingId;

            AssertSuccess(await moderator.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.CloseConference)));

            await WaitUntil(() => Recorder.Stopped.Contains(recordingId));
        }

        [Fact]
        public async Task RecorderReport_FullLifecycle_RecordingIsReady()
        {
            // arrange
            var (moderator, conference) = await ConnectToOpenedConference();
            AssertSuccess(await Start(moderator));
            var recordingId = Recorder.Started.Last(x => x.ConferenceId == conference.ConferenceId).RecordingId;

            // act & assert
            (await Report(recordingId, new RecorderReport(RecorderEvent.Started))).EnsureSuccessStatusCode();
            await moderator.SyncObjects.AssertSyncObject<SynchronizedRecording>(SynchronizedRecording.SyncObjId,
                recording => Assert.Equal(RecordingStatus.Recording, recording.Active!.Status));

            AssertSuccess(await Stop(moderator));
            (await Report(recordingId, new RecorderReport(RecorderEvent.Finished)
            {
                StorageKey = "key", SizeBytes = 100, DurationSeconds = 3,
            })).EnsureSuccessStatusCode();

            await moderator.SyncObjects.AssertSyncObject<SynchronizedRecording>(SynchronizedRecording.SyncObjId,
                recording => Assert.Null(recording.Active));

            var ready = (await GetRecordings(Moderator, conference.ConferenceId)).Single();
            Assert.Equal(RecordingStatus.Ready, ready.Status);
            Assert.Equal(100, ready.SizeBytes);
            Assert.Equal(3, ready.DurationSeconds);
            Assert.Equal(RecordingVisibility.SignedIn, ready.Visibility);
        }

        [Fact]
        public async Task RecorderReport_WrongSecret_ReturnUnauthorized()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            AssertSuccess(await Start(moderator));
            var recordingId = Recorder.Started.Last(x => x.ConferenceId == conference.ConferenceId).RecordingId;

            var response = await Report(recordingId, new RecorderReport(RecorderEvent.Started), "wrong");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task RecorderReport_NoSecret_ReturnUnauthorized()
        {
            var response = await CreateClient().PostAsync("/internal/recorder/x/report",
                new JsonNetContent(new RecorderReport(RecorderEvent.Started)));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task WireFormat_EnumsAreCamelCase_AsTheWebAppExpects()
        {
            // the web app compares these strings, a different spelling silently breaks the REC indicator and the list
            var (recording, _, conferenceId) = await RecordUntilReady(RecordingVisibility.AnyoneWithLink);

            var json = await CreateClient(Moderator).GetStringAsync($"/v1/conference/{conferenceId}/recordings");

            Assert.Contains("\"status\":\"ready\"", json);
            Assert.Contains("\"visibility\":\"anyoneWithLink\"", json);
            Assert.Contains($"\"shareToken\":\"{recording.ShareToken}\"", json);
        }

        [Fact]
        public async Task WireFormat_SynchronizedRecording_UsesCamelCaseStatus()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            AssertSuccess(await Start(moderator));

            var raw = await moderator.SyncObjects.WaitForSyncObj<SynchronizedRecording>(SynchronizedRecording.SyncObjId);

            // serialized like the server sends it to the web app
            var json = JsonConvert.SerializeObject(raw, JsonConfig.Default);
            Assert.Contains("\"status\":\"starting\"", json);
            Assert.Contains("\"active\":{", json);
            Assert.NotNull(conference);
        }

        [Fact]
        public async Task GetRecordings_NotModerator_ReturnForbidden()
        {
            var (_, conference) = await ConnectToOpenedConference();

            var response = await CreateClient(CreateUser()).GetAsync($"/v1/conference/{conference.ConferenceId}/recordings");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task SharedRecording_SignedInVisibility_RequiresLogin()
        {
            var (recording, _, _) = await RecordUntilReady();

            var anonymous = await CreateClient().GetAsync($"/v1/recordings/shared/{recording.ShareToken}");
            var signedIn = await CreateClient(CreateUser()).GetAsync($"/v1/recordings/shared/{recording.ShareToken}");

            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            var shared = await Read<SharedRecordingDto>(signedIn);
            Assert.StartsWith("https://storage.test/recordings/", shared.Url);
            Assert.Equal(12.5, shared.DurationSeconds);
        }

        [Fact]
        public async Task SharedRecording_AnyoneWithLink_AnonymousCanWatch()
        {
            var (recording, _, _) = await RecordUntilReady(RecordingVisibility.AnyoneWithLink);

            var response = await CreateClient().GetAsync($"/v1/recordings/shared/{recording.ShareToken}");

            var shared = await Read<SharedRecordingDto>(response);
            Assert.Contains("expires=3600", shared.Url);
        }

        [Fact]
        public async Task SharedRecording_UnknownToken_ReturnNotFound()
        {
            var response = await CreateClient(CreateUser()).GetAsync("/v1/recordings/shared/does-not-exist");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task SharedRecording_NotFinished_ReturnNotFoundEvenWithValidToken()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            AssertSuccess(await Start(moderator));
            var running = (await GetRecordings(Moderator, conference.ConferenceId)).Single();

            var response = await CreateClient(CreateUser()).GetAsync($"/v1/recordings/shared/{running.ShareToken}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task DeleteRecording_Moderator_DeleteFileAndRecord()
        {
            var (recording, _, conferenceId) = await RecordUntilReady();

            var response = await CreateClient(Moderator).DeleteAsync($"/v1/recordings/{recording.RecordingId}");

            response.EnsureSuccessStatusCode();
            Assert.Empty(await GetRecordings(Moderator, conferenceId));
            Assert.Contains($"recordings/{conferenceId}/{recording.RecordingId}.mp4", Storage.Deleted);
        }

        [Fact]
        public async Task DeleteRecording_NotModerator_ReturnForbidden()
        {
            var (recording, _, conferenceId) = await RecordUntilReady();

            var response = await CreateClient(CreateUser()).DeleteAsync($"/v1/recordings/{recording.RecordingId}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Single(await GetRecordings(Moderator, conferenceId));
        }

        [Fact]
        public async Task DeleteRecording_StillRecording_ReturnError()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            AssertSuccess(await Start(moderator));
            var running = (await GetRecordings(Moderator, conference.ConferenceId)).Single();

            var response = await CreateClient(Moderator).DeleteAsync($"/v1/recordings/{running.RecordingId}");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Single(await GetRecordings(Moderator, conference.ConferenceId));
        }

        [Fact]
        public async Task SetVisibility_NotModerator_ReturnForbidden()
        {
            var (recording, _, _) = await RecordUntilReady();

            var response = await CreateClient(CreateUser()).PatchAsync(
                $"/v1/recordings/{recording.RecordingId}/visibility",
                new JsonNetContent(new SetRecordingVisibilityDto(RecordingVisibility.AnyoneWithLink)));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        private static async Task WaitUntil(Func<bool> condition)
        {
            for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(100);
            Assert.True(condition(), "The condition was not met in time.");
        }
    }
}
