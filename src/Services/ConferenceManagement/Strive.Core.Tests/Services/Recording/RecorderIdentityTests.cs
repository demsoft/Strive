using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Strive.Core.Services;
using Strive.Core.Services.Permissions;
using Strive.Core.Services.Recording;
using Strive.Core.Services.Recording.Gateways;
using Strive.Core.Services.Recording.Requests;
using Strive.Core.Services.Recording.UseCases;
using Xunit;

namespace Strive.Core.Tests.Services.Recording
{
    public class RecorderIdentityTests
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1700000000);

        private readonly Mock<IRecordingRepo> _repository = new();

        [Theory]
        [InlineData("recorder-abc", true)]
        [InlineData("recorder-", true)]
        [InlineData("56696E63656E74", false)]
        [InlineData("Recorder-abc", false)]
        [InlineData("my-recorder-abc", false)]
        public void IsRecorder(string id, bool expected)
        {
            Assert.Equal(expected, RecorderParticipants.IsRecorder(id));
        }

        [Fact]
        public async Task PermissionLayer_Recorder_DeniesEveryBooleanPermission()
        {
            var layers = (await new RecorderPermissionLayerProvider().FetchPermissionsForParticipant(
                new Participant("c", RecorderParticipants.ParticipantId("r1")))).ToList();

            var layer = Assert.Single(layers);
            Assert.Equal(RecorderPermissionLayerProvider.Order, layer.Order);
            var booleans = DefinedPermissionsProvider.All.Values.Where(x => x.Type == PermissionValueType.Boolean);
            foreach (var permission in booleans)
                Assert.False((bool) layer.Permissions[permission.Key].Value!, permission.Key);
            Assert.Contains(DefinedPermissions.Recording.CanManage.Key, layer.Permissions.Keys);
            Assert.Contains(DefinedPermissions.Media.CanShareAudio.Key, layer.Permissions.Keys);
        }

        [Fact]
        public async Task PermissionLayer_RecorderLayerWinsOverEveryOtherLayer()
        {
            // the highest order is applied last
            Assert.True(RecorderPermissionLayerProvider.Order > 100);
            var layers = await new RecorderPermissionLayerProvider().FetchPermissionsForParticipant(
                new Participant("c", "someone"));
            Assert.Empty(layers);
        }

        private ConferenceRecording CreateRecording(RecordingStatus status, string conferenceId = "c")
        {
            return new ConferenceRecording("r1", conferenceId, "mod", Now, Now.AddDays(1), "token") {Status = status};
        }

        private Task<bool> MayJoin(string participantId, string conferenceId = "c", string recordingId = "r1")
        {
            return new CheckRecorderMayJoinUseCase(_repository.Object).Handle(
                new CheckRecorderMayJoinRequest(new Participant(conferenceId, participantId), recordingId), default);
        }

        [Theory]
        [InlineData(RecordingStatus.Starting)]
        [InlineData(RecordingStatus.Recording)]
        public async Task MayJoin_RecordingRunning_True(RecordingStatus status)
        {
            _repository.Setup(x => x.FindById("r1")).ReturnsAsync(CreateRecording(status));

            Assert.True(await MayJoin(RecorderParticipants.ParticipantId("r1")));
        }

        [Theory]
        [InlineData(RecordingStatus.Finalizing)]
        [InlineData(RecordingStatus.Ready)]
        [InlineData(RecordingStatus.Failed)]
        public async Task MayJoin_RecordingOver_False(RecordingStatus status)
        {
            _repository.Setup(x => x.FindById("r1")).ReturnsAsync(CreateRecording(status));

            Assert.False(await MayJoin(RecorderParticipants.ParticipantId("r1")));
        }

        [Fact]
        public async Task MayJoin_OtherConference_False()
        {
            _repository.Setup(x => x.FindById("r1")).ReturnsAsync(CreateRecording(RecordingStatus.Recording, "other"));

            Assert.False(await MayJoin(RecorderParticipants.ParticipantId("r1")));
        }

        [Fact]
        public async Task MayJoin_ParticipantIdBelongsToAnotherRecording_False()
        {
            _repository.Setup(x => x.FindById("r1")).ReturnsAsync(CreateRecording(RecordingStatus.Recording));

            Assert.False(await MayJoin(RecorderParticipants.ParticipantId("r2")));
        }

        [Fact]
        public async Task MayJoin_UnknownRecording_False()
        {
            _repository.Setup(x => x.FindById("r1")).ReturnsAsync((ConferenceRecording?) null);

            Assert.False(await MayJoin(RecorderParticipants.ParticipantId("r1")));
        }
    }
}
