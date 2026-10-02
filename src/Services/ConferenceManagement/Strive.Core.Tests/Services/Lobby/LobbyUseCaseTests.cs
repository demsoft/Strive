using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediatR;
using Moq;
using Strive.Core.Domain.Entities;
using Strive.Core.Services;
using Strive.Core.Services.ConferenceControl.Gateways;
using Strive.Core.Services.ConferenceControl.Notifications;
using Strive.Core.Services.ConferenceManagement.Requests;
using Strive.Core.Services.Lobby;
using Strive.Core.Services.Lobby.Gateways;
using Strive.Core.Services.Lobby.NotificationHandlers;
using Strive.Core.Services.Lobby.Notifications;
using Strive.Core.Services.Lobby.Requests;
using Strive.Core.Services.Lobby.UseCases;
using Strive.Core.Services.Synchronization.Requests;
using Strive.Tests.Utils;
using Xunit;

namespace Strive.Core.Tests.Services.Lobby
{
    public class LobbyUseCaseTests
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1700000000);

        private readonly Participant _participant = new("conference1", "participant1");
        private readonly LobbyEntry _entry = new("connection1", "Alice", Now);
        private readonly Mock<ILobbyRepository> _repository = new();
        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<IOpenConferenceRepository> _openConferences = new();

        private class FixedTimeProvider : TimeProvider
        {
            public override DateTimeOffset GetUtcNow()
            {
                return Now;
            }
        }

        private Task<bool> ShouldWait(bool lobbyEnabled = true, bool moderator = false, bool open = true,
            bool admitted = false)
        {
            var conference = new Conference("conference1")
            {
                Configuration = new ConferenceConfiguration
                {
                    Lobby = new LobbyOptions {IsEnabled = lobbyEnabled},
                    Moderators = moderator ? new List<string> {_participant.Id} : new List<string>(),
                },
            };
            _mediator.Setup(x => x.Send(It.IsAny<FindConferenceByIdRequest>(), default)).ReturnsAsync(conference);
            _openConferences.Setup(x => x.IsOpen("conference1")).ReturnsAsync(open);
            _repository.Setup(x => x.IsAdmitted(_participant)).ReturnsAsync(admitted);

            return new ShouldWaitInLobbyUseCase(_mediator.Object, _openConferences.Object, _repository.Object)
                .Handle(new ShouldWaitInLobbyRequest(_participant), default);
        }

        [Fact]
        public async Task ShouldWait_LobbyEnabledForParticipant_ReturnTrue()
        {
            Assert.True(await ShouldWait());
        }

        [Fact]
        public async Task ShouldWait_LobbyDisabled_ReturnFalse()
        {
            Assert.False(await ShouldWait(lobbyEnabled: false));
        }

        [Fact]
        public async Task ShouldWait_Moderator_ReturnFalse()
        {
            Assert.False(await ShouldWait(moderator: true));
        }

        [Fact]
        public async Task ShouldWait_ConferenceNotOpen_ReturnFalse()
        {
            Assert.False(await ShouldWait(open: false));
        }

        [Fact]
        public async Task ShouldWait_AlreadyAdmitted_ReturnFalse()
        {
            Assert.False(await ShouldWait(admitted: true));
        }

        [Fact]
        public async Task Enter_StoreEntryWithCurrentTimeAndUpdateSyncObject()
        {
            var update = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            await new EnterLobbyUseCase(_repository.Object, _mediator.Object, new FixedTimeProvider())
                .Handle(new EnterLobbyRequest(_participant, "connection1", "Alice"), default);

            _repository.Verify(x => x.Add(_participant, _entry), Times.Once);
            update.AssertReceived();
            Assert.Equal(SynchronizedLobby.SyncObjId, update.GetRequest().SynchronizedObjectId);
        }

        [Fact]
        public async Task Leave_ConnectionWasWaiting_UpdateSyncObject()
        {
            _repository.Setup(x => x.TryRemove(_participant, "connection1")).ReturnsAsync(_entry);
            var update = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            await new LeaveLobbyUseCase(_repository.Object, _mediator.Object)
                .Handle(new LeaveLobbyRequest(_participant, "connection1"), default);

            update.AssertReceived();
        }

        [Fact]
        public async Task Leave_ConnectionWasNotWaiting_DoNotUpdateSyncObject()
        {
            _repository.Setup(x => x.TryRemove(_participant, "connection1")).ReturnsAsync((LobbyEntry?) null);
            var update = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            await new LeaveLobbyUseCase(_repository.Object, _mediator.Object)
                .Handle(new LeaveLobbyRequest(_participant, "connection1"), default);

            update.AssertNotReceived();
        }

        [Fact]
        public async Task Admit_Waiting_MarkAdmittedAndNotify()
        {
            _repository.Setup(x => x.TryRemove(_participant, null)).ReturnsAsync(_entry);
            var update = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            var result = await new AdmitParticipantUseCase(_repository.Object, _mediator.Object)
                .Handle(new AdmitParticipantRequest(_participant), default);

            Assert.True(result.Success);
            _repository.Verify(x => x.MarkAdmitted(_participant), Times.Once);
            update.AssertReceived();
            _mediator.Verify(
                x => x.Publish(
                    It.Is<ParticipantAdmittedNotification>(n =>
                        n.Participant.Equals(_participant) && n.ConnectionId == "connection1" &&
                        n.DisplayName == "Alice"), default), Times.Once);
        }

        [Fact]
        public async Task Admit_NotWaiting_ReturnErrorAndDoNothing()
        {
            _repository.Setup(x => x.TryRemove(_participant, null)).ReturnsAsync((LobbyEntry?) null);

            var result = await new AdmitParticipantUseCase(_repository.Object, _mediator.Object)
                .Handle(new AdmitParticipantRequest(_participant), default);

            Assert.False(result.Success);
            Assert.Equal(nameof(ServiceErrorCode.Lobby_ParticipantNotWaiting), result.Error!.Code);
            _repository.Verify(x => x.MarkAdmitted(It.IsAny<Participant>()), Times.Never);
            _mediator.Verify(x => x.Publish(It.IsAny<ParticipantAdmittedNotification>(), default), Times.Never);
        }

        [Fact]
        public async Task Deny_Waiting_NotifyAndDoNotAdmit()
        {
            _repository.Setup(x => x.TryRemove(_participant, null)).ReturnsAsync(_entry);
            var update = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            var result = await new DenyParticipantUseCase(_repository.Object, _mediator.Object)
                .Handle(new DenyParticipantRequest(_participant), default);

            Assert.True(result.Success);
            update.AssertReceived();
            _repository.Verify(x => x.MarkAdmitted(It.IsAny<Participant>()), Times.Never);
            _mediator.Verify(
                x => x.Publish(
                    It.Is<ParticipantDeniedNotification>(n =>
                        n.Participant.Equals(_participant) && n.ConnectionId == "connection1"), default),
                Times.Once);
        }

        [Fact]
        public async Task Deny_NotWaiting_ReturnError()
        {
            _repository.Setup(x => x.TryRemove(_participant, null)).ReturnsAsync((LobbyEntry?) null);

            var result = await new DenyParticipantUseCase(_repository.Object, _mediator.Object)
                .Handle(new DenyParticipantRequest(_participant), default);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task AdmitAll_AdmitEveryoneWaiting()
        {
            _repository.Setup(x => x.GetAll("conference1")).ReturnsAsync(new Dictionary<string, LobbyEntry>
            {
                {"a", _entry}, {"b", _entry},
            });
            var admitted = new List<Participant>();
            _mediator.Setup(x => x.Send(It.IsAny<AdmitParticipantRequest>(), default))
                .Callback<IRequest<Strive.Core.Interfaces.SuccessOrError<Unit>>, System.Threading.CancellationToken>(
                    (r, _) => admitted.Add(((AdmitParticipantRequest) r).Participant))
                .ReturnsAsync(Unit.Value);

            await new AdmitAllParticipantsUseCase(_repository.Object, _mediator.Object)
                .Handle(new AdmitAllParticipantsRequest("conference1"), default);

            Assert.Equal(new[] {new Participant("conference1", "a"), new Participant("conference1", "b")}, admitted);
        }

        [Theory]
        [InlineData(ParticipantKickedReason.ByModerator, 1)]
        [InlineData(ParticipantKickedReason.NewSessionConnected, 0)]
        public async Task Kicked_OnlyByModeratorRemovesAdmission(ParticipantKickedReason reason, int removals)
        {
            await new ParticipantKickedNotificationHandler(_repository.Object)
                .Handle(new ParticipantKickedNotification(_participant, "connection1", reason), default);

            _repository.Verify(x => x.RemoveAdmitted(_participant), Times.Exactly(removals));
        }
    }
}
