using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using Strive.Core.Services;
using Strive.Core.Services.ConferenceControl.Gateways;
using Strive.Core.Services.Equipment;
using Strive.Core.Services.Equipment.Gateways;
using Strive.Core.Services.Equipment.Requests;
using Strive.Core.Services.Equipment.UseCases;
using Strive.Core.Services.Media.Dtos;
using Strive.Core.Services.Synchronization.Requests;
using Strive.Infrastructure.KeyValue.Abstractions;
using Strive.Tests.Utils;
using Xunit;

namespace Strive.Core.Tests.Services.Equipment.UseCases
{
    public class UpdateStatusUseCaseTests
    {
        private readonly Mock<IEquipmentConnectionRepository> _repo = new();
        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<IJoinedParticipantsRepository> _joinedParticipants = new();
        private readonly List<string> _events = new();

        private readonly Participant _testParticipant = new("123", "435");
        private const string ConnectionId = "test";

        private UpdateStatusUseCase Create()
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

        [Fact]
        public async Task Handle_ConnectionDoesNotExist_ThrowException()
        {
            // arrange
            var useCase = Create();

            // act
            await Assert.ThrowsAnyAsync<Exception>(async () => await useCase.Handle(
                new UpdateStatusRequest(_testParticipant, ConnectionId,
                    ImmutableDictionary<ProducerSource, UseMediaStateInfo>.Empty), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ConnectionExists_UpdateInDatabase()
        {
            // arrange
            var useCase = Create();
            var existingConnection = new EquipmentConnection(ConnectionId, "Smartphone", Array.Empty<EquipmentDevice>(),
                ImmutableDictionary<ProducerSource, UseMediaStateInfo>.Empty);

            _repo.Setup(x => x.GetConnection(_testParticipant, ConnectionId)).ReturnsAsync(existingConnection);

            EquipmentConnection? addedConnection = null;
            _repo.Setup(x => x.SetConnection(_testParticipant, It.IsAny<EquipmentConnection>()))
                .Callback((Participant _, EquipmentConnection conn) => addedConnection = conn);

            var newMediaState = new Dictionary<ProducerSource, UseMediaStateInfo>
                {{ProducerSource.Mic, new UseMediaStateInfo(true, false, false, null)}};

            // act
            await useCase.Handle(new UpdateStatusRequest(_testParticipant, ConnectionId, newMediaState),
                CancellationToken.None);

            // assert
            Assert.NotNull(addedConnection);

            Assert.Equal(existingConnection.ConnectionId, addedConnection!.ConnectionId);
            Assert.Equal(existingConnection.Name, addedConnection!.Name);
            Assert.Equal(existingConnection.Devices, addedConnection!.Devices);
            Assert.Equal(newMediaState, addedConnection!.Status);
        }

        [Fact]
        public async Task Handle_ConnectionExists_UpdateSyncObj()
        {
            // arrange
            var useCase = Create();
            var existingConnection = new EquipmentConnection(ConnectionId, "Smartphone", Array.Empty<EquipmentDevice>(),
                ImmutableDictionary<ProducerSource, UseMediaStateInfo>.Empty);

            _repo.Setup(x => x.GetConnection(_testParticipant, ConnectionId)).ReturnsAsync(existingConnection);

            var capturedRequest = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            // act
            await useCase.Handle(
                new UpdateStatusRequest(_testParticipant, ConnectionId,
                    ImmutableDictionary<ProducerSource, UseMediaStateInfo>.Empty), CancellationToken.None);

            // assert
            capturedRequest.AssertReceived();

            Assert.Equal($"equipment?participantId={_testParticipant.Id}",
                capturedRequest.GetRequest().SynchronizedObjectId.ToString());
        }

        [Fact]
        public async Task Handle_ConnectionExists_ReadAndWriteWhileHoldingTheParticipantLock()
        {
            // arrange
            var useCase = Create();
            var connection = new EquipmentConnection(ConnectionId, "test", Array.Empty<EquipmentDevice>(),
                ImmutableDictionary<ProducerSource, UseMediaStateInfo>.Empty);

            _repo.Setup(x => x.GetConnection(_testParticipant, ConnectionId)).Callback(() => _events.Add("get"))
                .ReturnsAsync(connection);
            _repo.Setup(x => x.SetConnection(_testParticipant, It.IsAny<EquipmentConnection>()))
                .Callback(() => _events.Add("set"));

            // act
            await useCase.Handle(
                new UpdateStatusRequest(_testParticipant, ConnectionId,
                    ImmutableDictionary<ProducerSource, UseMediaStateInfo>.Empty), CancellationToken.None);

            // assert
            Assert.Equal(new[] {"lock", "get", "set", "unlock"}, _events);
        }
    }
}
