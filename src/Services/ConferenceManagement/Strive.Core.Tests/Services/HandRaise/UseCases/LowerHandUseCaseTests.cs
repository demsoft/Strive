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
    public class LowerHandUseCaseTests
    {
        private readonly Participant _participant = new("123", "participant");
        private readonly Mock<IHandRaiseRepository> _repository = new();
        private readonly Mock<IJoinedParticipantsRepository> _joinedParticipants = new();
        private readonly Mock<IMediator> _mediator = new();
        private readonly List<string> _events = new();

        private LowerHandUseCase Create()
        {
            var acquiredLock = new Mock<IAcquiredLock>();
            acquiredLock.Setup(x => x.DisposeAsync()).Callback(() => _events.Add("unlock"))
                .Returns(ValueTask.CompletedTask);
            _joinedParticipants.Setup(x => x.LockParticipantJoin(_participant)).Callback(() => _events.Add("lock"))
                .ReturnsAsync(acquiredLock.Object);

            return new LowerHandUseCase(_repository.Object, _joinedParticipants.Object, _mediator.Object);
        }

        [Fact]
        public async Task Handle_HandRaised_LowerAndUpdateSyncObject()
        {
            // arrange
            var useCase = Create();
            _repository.Setup(x => x.Lower(_participant)).ReturnsAsync(true);
            var update = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            // act
            await useCase.Handle(new LowerHandRequest(_participant), CancellationToken.None);

            // assert
            var request = update.GetRequest();
            Assert.Equal(_participant.ConferenceId, request.ConferenceId);
            Assert.Equal(SynchronizedHandRaises.SyncObjId, request.SynchronizedObjectId);
        }

        [Fact]
        public async Task Handle_HandNotRaised_DoNotUpdateSyncObject()
        {
            // arrange
            var useCase = Create();
            _repository.Setup(x => x.Lower(_participant)).ReturnsAsync(false);
            var update = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            // act
            await useCase.Handle(new LowerHandRequest(_participant), CancellationToken.None);

            // assert
            update.AssertNotReceived();
        }

        [Fact]
        public async Task Handle_LowerWhileHoldingTheParticipantLock()
        {
            // arrange
            var useCase = Create();
            _repository.Setup(x => x.Lower(_participant)).Callback(() => _events.Add("lower")).ReturnsAsync(true);

            // act
            await useCase.Handle(new LowerHandRequest(_participant), CancellationToken.None);

            // assert
            Assert.Equal(new[] {"lock", "lower", "unlock"}, _events);
        }
    }
}
