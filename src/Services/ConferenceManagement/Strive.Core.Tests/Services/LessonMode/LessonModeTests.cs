using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using Strive.Core.Services;
using Strive.Core.Services.ConferenceControl.Gateways;
using Strive.Core.Services.LessonMode;
using Strive.Core.Services.LessonMode.Gateways;
using Strive.Core.Services.LessonMode.Requests;
using Strive.Core.Services.LessonMode.UseCases;
using Strive.Core.Services.Permissions;
using Strive.Core.Services.Permissions.Requests;
using Strive.Core.Services.Synchronization.Requests;
using Xunit;

namespace Strive.Core.Tests.Services.LessonMode
{
    public class LessonModeTests
    {
        private const string ConferenceId = "conference1";

        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<ILessonModeRepository> _repository = new();
        private readonly Mock<IJoinedParticipantsRepository> _participants = new();
        private bool _active;

        public LessonModeTests()
        {
            _repository.Setup(x => x.IsActive(ConferenceId)).Returns(() => new ValueTask<bool>(_active));
            _repository.Setup(x => x.Set(ConferenceId, It.IsAny<bool>())).Returns((string _, bool value) =>
            {
                var changed = _active != value;
                _active = value;
                return new ValueTask<bool>(changed);
            });
            _participants.Setup(x => x.GetParticipantsOfConference(ConferenceId)).Returns(
                new ValueTask<IEnumerable<Participant>>(new[]
                    {new Participant(ConferenceId, "teacher"), new Participant(ConferenceId, "student")}));
        }

        private SetLessonModeUseCase CreateUseCase() => new(_mediator.Object, _repository.Object, _participants.Object);

        [Fact]
        public async Task Handle_TurnOn_UpdatesPermissionsOfEverybodyAndSynchronizes()
        {
            await CreateUseCase().Handle(new SetLessonModeRequest(ConferenceId, true), CancellationToken.None);

            Assert.True(_active);
            _mediator.Verify(x => x.Send(
                It.Is<UpdateParticipantsPermissionsRequest>(r => r.Participants.Count() == 2),
                It.IsAny<CancellationToken>()));
            _mediator.Verify(x => x.Send(
                It.Is<UpdateSynchronizedObjectRequest>(r => r.SynchronizedObjectId.Id == SynchronizedLessonMode.SyncObjId.Id),
                It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task Handle_AlreadyOn_DoesNothing()
        {
            _active = true;

            await CreateUseCase().Handle(new SetLessonModeRequest(ConferenceId, true), CancellationToken.None);

            _mediator.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task LayerProvider_Active_DisablesAudioAboveTheConferencePermissionsAndBelowModerators()
        {
            _active = true;
            var provider = new LessonModePermissionLayerProvider(_repository.Object);

            var layers = (await provider.FetchPermissionsForParticipant(new Participant(ConferenceId, "student")))
                .ToList();

            var layer = Assert.Single(layers);
            Assert.True(layer.Order > CommonPermissionLayers.Conference(null!).Order);
            Assert.True(layer.Order < CommonPermissionLayers.ModeratorDefault(null!).Order);
            Assert.False((bool) layer.Permissions[DefinedPermissions.Media.CanShareAudio.Key].Value!);
        }

        [Fact]
        public async Task LayerProvider_NotActive_NoLayer()
        {
            var provider = new LessonModePermissionLayerProvider(_repository.Object);

            var layers = await provider.FetchPermissionsForParticipant(new Participant(ConferenceId, "student"));

            Assert.Empty(layers);
        }
    }
}
