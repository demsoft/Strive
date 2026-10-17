using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json.Linq;
using Strive.Core.Interfaces;
using Strive.Core.Services;
using Strive.Core.Services.LessonMode;
using Strive.Core.Services.Permissions;
using Strive.Hubs.Core;
using Strive.Hubs.Core.Dtos;
using Strive.IntegrationTests._Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    [Collection(IntegrationTestCollection.Definition)]
    public class LessonModeTests : ServiceIntegrationTest
    {
        private static readonly string AudioKey = DefinedPermissions.Media.CanShareAudio.Key;

        public LessonModeTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper,
            mongoDb)
        {
        }

        private static Task<SuccessOrError<Unit>> SetLessonMode(UserConnection connection, bool active) =>
            connection.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.SetLessonMode),
                new SetLessonModeDto(active));

        private static Task AssertCanShareAudio(UserConnection connection, bool expected) =>
            connection.SyncObjects.AssertSyncObject<SynchronizedParticipantPermissions>(
                SynchronizedParticipantPermissions.SyncObjId(connection.User.Sub),
                value => Assert.Equal(expected, value.Permissions[AudioKey].Value<bool>()));

        [Fact]
        public async Task SetLessonMode_Moderator_ParticipantsAreMutedAndTheModeratorStaysAudible()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            var student = await ConnectUserToConference(CreateUser(), conference);
            await AssertCanShareAudio(student, true);

            AssertSuccess(await SetLessonMode(moderator, true));

            await AssertCanShareAudio(student, false);
            await AssertCanShareAudio(moderator, true);
            await student.SyncObjects.AssertSyncObject<SynchronizedLessonMode>(SynchronizedLessonMode.SyncObjId,
                value => Assert.True(value.Active));
        }

        [Fact]
        public async Task SetLessonMode_JoinsLater_IsMutedToo()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            AssertSuccess(await SetLessonMode(moderator, true));

            var late = await ConnectUserToConference(CreateUser(), conference);

            await AssertCanShareAudio(late, false);
        }

        [Fact]
        public async Task SetLessonMode_Off_EverybodyCanSpeakAgain()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            var student = await ConnectUserToConference(CreateUser(), conference);
            AssertSuccess(await SetLessonMode(moderator, true));
            await AssertCanShareAudio(student, false);

            AssertSuccess(await SetLessonMode(moderator, false));

            await AssertCanShareAudio(student, true);
        }

        [Fact]
        public async Task SetLessonMode_StudentWithTemporaryPermission_CanSpeak()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            var student = await ConnectUserToConference(CreateUser(), conference);
            AssertSuccess(await SetLessonMode(moderator, true));
            await AssertCanShareAudio(student, false);

            AssertSuccess(await moderator.Hub.InvokeAsync<SuccessOrError<Unit>>(
                nameof(CoreHub.SetTemporaryPermission),
                new SetTemporaryPermissionDto(student.User.Sub, AudioKey, (JValue) JToken.FromObject(true))));

            await AssertCanShareAudio(student, true);
        }

        [Fact]
        public async Task SetLessonMode_ParticipantWithoutPermission_ReturnPermissionDenied()
        {
            var (_, conference) = await ConnectToOpenedConference();
            var student = await ConnectUserToConference(CreateUser(), conference);

            var result = await SetLessonMode(student, true);

            AssertErrorCode(ServiceErrorCode.PermissionDenied, result.Error!);
        }
    }
}
