using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using Strive.Core.Services;
using Strive.Core.Services.ConferenceControl.Gateways;
using Strive.Core.Services.HandRaise;
using Strive.Core.Services.HandRaise.Gateways;
using Strive.Core.Services.HandRaise.Requests;
using Strive.Core.Services.HandRaise.UseCases;
using Strive.Core.Services.Synchronization.Requests;
using Strive.Infrastructure.KeyValue.Abstractions;
using Strive.Tests.Utils;
using Xunit;

namespace Strive.Core.Tests.Services.HandRaise.UseCases
{
    public class RaiseHandUseCaseTests
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1700000000);

        private readonly Participant _participant = new("123", "participant");
        private readonly Mock<IHandRaiseRepository> _repository = new();
        private readonly Mock<IJoinedParticipantsRepository> _joinedParticipants = new();
        private readonly Mock<IMediator> _mediator = new();
        private readonly List<string> _events = new();

        private class FixedTimeProvider : TimeProvider
        {
            public override DateTimeOffset GetUtcNow()
            {
                return Now;
            }
        }

        private RaiseHandUseCase Create(bool joined = true)
        {
            var acquiredLock = new Mock<IAcquiredLock>();
            acquiredLock.Setup(x => x.DisposeAsync()).Callback(() => _events.Add("unlock"))
                .Returns(ValueTask.CompletedTask);
            _joinedParticipants.Setup(x => x.LockParticipantJoin(_participant)).Callback(() => _events.Add("lock"))
                .ReturnsAsync(acquiredLock.Object);
            _joinedParticipants.Setup(x => x.IsParticipantJoined(_participant)).Callback(() => _events.Add("joined?"))
                .ReturnsAsync(joined);

            return new RaiseHandUseCase(_repository.Object, _joinedParticipants.Object, _mediator.Object,
                new FixedTimeProvider());
        }

        [Fact]
        public async Task Handle_HandNotRaised_RaiseWithCurrentTimeAndUpdateSyncObject()
        {
            // arrange
            var useCase = Create();
            _repository.Setup(x => x.Raise(_participant, Now)).ReturnsAsync(true);
            var update = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            // act
            await useCase.Handle(new RaiseHandRequest(_participant), CancellationToken.None);

            // assert
            _repository.Verify(x => x.Raise(_participant, Now), Times.Once);

            var request = update.GetRequest();
            Assert.Equal(_participant.ConferenceId, request.ConferenceId);
            Assert.Equal(SynchronizedHandRaises.SyncObjId, request.SynchronizedObjectId);
        }

        [Fact]
        public async Task Handle_HandAlreadyRaised_DoNotUpdateSyncObject()
        {
            // arrange
            var useCase = Create();
            _repository.Setup(x => x.Raise(_participant, It.IsAny<DateTimeOffset>())).ReturnsAsync(false);
            var update = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            // act
            await useCase.Handle(new RaiseHandRequest(_participant), CancellationToken.None);

            // assert
            update.AssertNotReceived();
        }

        [Fact]
        public async Task Handle_ParticipantNotJoined_DoNothing()
        {
            // arrange
            var useCase = Create(false);
            var update = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            // act
            await useCase.Handle(new RaiseHandRequest(_participant), CancellationToken.None);

            // assert
            _repository.Verify(x => x.Raise(It.IsAny<Participant>(), It.IsAny<DateTimeOffset>()), Times.Never);
            update.AssertNotReceived();
        }

        [Fact]
        public async Task Handle_CheckJoinedAndRaiseWhileHoldingTheParticipantLock()
        {
            // arrange
            var useCase = Create();
            _repository.Setup(x => x.Raise(_participant, It.IsAny<DateTimeOffset>())).Callback(() => _events.Add("raise"))
                .ReturnsAsync(true);

            // act
            await useCase.Handle(new RaiseHandRequest(_participant), CancellationToken.None);

            // assert
            Assert.Equal(new[] {"lock", "joined?", "raise", "unlock"}, _events);
        }
    }
}
