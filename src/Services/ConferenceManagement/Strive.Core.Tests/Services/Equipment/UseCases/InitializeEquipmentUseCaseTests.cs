using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using Strive.Core.Services;
using Strive.Core.Services.ConferenceControl.Gateways;
using Strive.Core.Services.ConferenceControl.Requests;
using Strive.Core.Services.Equipment;
using Strive.Core.Services.Equipment.Gateways;
using Strive.Core.Services.Equipment.Requests;
using Strive.Core.Services.Equipment.UseCases;
using Strive.Core.Services.Synchronization.Requests;
using Strive.Infrastructure.KeyValue.Abstractions;
using Xunit;

namespace Strive.Core.Tests.Services.Equipment.UseCases
{
    public class InitializeEquipmentUseCaseTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<IEquipmentConnectionRepository> _repo = new();
        private readonly Mock<IJoinedParticipantsRepository> _joinedParticipants = new();
        private readonly List<string> _events = new();

        private readonly Participant _testParticipant = new("123", "wtf");
        private const string ConnectionId = "connId";
        private const string DeviceName = "Smartphone";

        private InitializeEquipmentUseCase Create()
        {
            SetupParticipantLock();
            return new(_repo.Object, _joinedParticipants.Object, _mediator.Object);
        }

        private void SetupParticipantLock()
        {
            var acquiredLock = new Mock<IAcquiredLock>();
            acquiredLock.Setup(x => x.DisposeAsync()).Callback(() => _events.Add("unlock"))
                .Returns(ValueTask.CompletedTask);
            _joinedParticipants.Setup(x => x.LockParticipantJoin(_testParticipant))
                .Callback(() => _events.Add("lock")).ReturnsAsync(acquiredLock.Object);
        }

        private void SetupIsParticipantJoined(Participant participant, bool joined)
        {
            _mediator.Setup(x =>
                x.Send(It.Is<CheckIsParticipantJoinedRequest>(request => request.Participant.Equals(participant)),
                    It.IsAny<CancellationToken>())).ReturnsAsync(joined);
        }

        [Fact]
        public async Task Handle_ParticipantJoined_AddConnectionInRepo()
        {
            // arrange
            var useCase = Create();

            var testDevice = new EquipmentDevice("123", "Smartphone Microphone", DeviceType.Mic);
            var request =
                new InitializeEquipmentRequest(_testParticipant, ConnectionId, DeviceName, new[] {testDevice});

            SetupIsParticipantJoined(_testParticipant, true);

            EquipmentConnection? addedConnection = null;
            _repo.Setup(x => x.SetConnection(_testParticipant, It.IsAny<EquipmentConnection>()))
                .Callback((Participant _, EquipmentConnection conn) => addedConnection = conn);

            // act
            await useCase.Handle(request, CancellationToken.None);

            // assert
            _repo.Verify(x => x.SetConnection(_testParticipant, It.IsAny<EquipmentConnection>()), Times.Once);

            Assert.NotNull(addedConnection);
            Assert.Equal(DeviceName, addedConnection!.Name);
            Assert.Equal(ConnectionId, addedConnection!.ConnectionId);
            Assert.Empty(addedConnection!.Status);

            var device = Assert.Single(addedConnection!.Devices);
            Assert.Equal(testDevice, device);
        }

        [Fact]
        public async Task Handle_ParticipantNotJoined_ThrowExceptionAndDoNotAddConnection()
        {
            // arrange
            var useCase = Create();

            var request = new InitializeEquipmentRequest(_testParticipant, ConnectionId, DeviceName,
                Array.Empty<EquipmentDevice>());

            SetupIsParticipantJoined(_testParticipant, false);

            // act
            await Assert.ThrowsAnyAsync<Exception>(async () => await useCase.Handle(request, CancellationToken.None));
            _repo.Verify(x => x.SetConnection(It.IsAny<Participant>(), It.IsAny<EquipmentConnection>()), Times.Never);
            Assert.Equal(new[] {"lock", "unlock"}, _events);
        }

        [Fact]
        public async Task Handle_ParticipantJoined_CheckAndAddConnectionWhileHoldingTheParticipantLock()
        {
            // arrange
            var useCase = Create();
            var request = new InitializeEquipmentRequest(_testParticipant, ConnectionId, DeviceName,
                Array.Empty<EquipmentDevice>());

            _mediator.Setup(x =>
                    x.Send(It.IsAny<CheckIsParticipantJoinedRequest>(), It.IsAny<CancellationToken>()))
                .Callback(() => _events.Add("check")).ReturnsAsync(true);
            _repo.Setup(x => x.SetConnection(_testParticipant, It.IsAny<EquipmentConnection>()))
                .Callback(() => _events.Add("set"));

            // act
            await useCase.Handle(request, CancellationToken.None);

            // assert
            Assert.Equal(new[] {"lock", "check", "set", "unlock"}, _events);
        }

        [Fact]
        public async Task Handle_ParticipantJoined_UpdateSyncObject()
        {
            // arrange
            var useCase = Create();

            var request = new InitializeEquipmentRequest(_testParticipant, ConnectionId, DeviceName,
                Array.Empty<EquipmentDevice>());

            SetupIsParticipantJoined(_testParticipant, true);

            // act
            await useCase.Handle(request, CancellationToken.None);
            _mediator.Verify(
                x => x.Send(
                    It.Is<UpdateSynchronizedObjectRequest>(x =>
                        x.SynchronizedObjectId.ToString() == $"equipment?participantId={_testParticipant.Id}"),
                    It.IsAny<CancellationToken>()), Times.Once());
        }
    }
}
