using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using Strive.Core.Services;
using Strive.Core.Services.ConferenceControl.Notifications;
using Strive.Core.Services.HandRaise;
using Strive.Core.Services.HandRaise.Gateways;
using Strive.Core.Services.HandRaise.NotificationHandlers;
using Strive.Core.Services.Synchronization.Requests;
using Strive.Tests.Utils;
using Xunit;

namespace Strive.Core.Tests.Services.HandRaise.NotificationHandlers
{
    public class ParticipantLeftNotificationHandlerTests
    {
        private readonly Participant _participant = new("123", "participant");
        private readonly Mock<IHandRaiseRepository> _repository = new();
        private readonly Mock<IMediator> _mediator = new();

        private ParticipantLeftNotificationHandler Create()
        {
            return new(_repository.Object, _mediator.Object);
        }

        [Fact]
        public async Task Handle_HandRaised_LowerAndUpdateSyncObject()
        {
            // arrange
            _repository.Setup(x => x.Lower(_participant)).ReturnsAsync(true);
            var update = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            // act
            await Create().Handle(new ParticipantLeftNotification(_participant, "connection"), CancellationToken.None);

            // assert
            Assert.Equal(SynchronizedHandRaises.SyncObjId, update.GetRequest().SynchronizedObjectId);
        }

        [Fact]
        public async Task Handle_HandNotRaised_DoNotUpdateSyncObject()
        {
            // arrange
            _repository.Setup(x => x.Lower(_participant)).ReturnsAsync(false);
            var update = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            // act
            await Create().Handle(new ParticipantLeftNotification(_participant, "connection"), CancellationToken.None);

            // assert
            update.AssertNotReceived();
        }
    }
}
