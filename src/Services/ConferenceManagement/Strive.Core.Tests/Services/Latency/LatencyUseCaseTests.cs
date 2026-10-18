using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using Strive.Core.Services;
using Strive.Core.Services.ConferenceControl.Notifications;
using Strive.Core.Services.Latency;
using Strive.Core.Services.Latency.Gateways;
using Strive.Core.Services.Latency.NotificationHandlers;
using Strive.Core.Services.Latency.Requests;
using Strive.Core.Services.Latency.UseCases;
using Strive.Core.Services.Synchronization.Requests;
using Xunit;

namespace Strive.Core.Tests.Services.Latency
{
    public class LatencyUseCaseTests
    {
        private const string ConferenceId = "conference1";
        private readonly Participant _musician = new(ConferenceId, "musician1");
        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<ILatencyRepository> _repository = new();

        private ReportLatencyUseCase Create() => new(_mediator.Object, _repository.Object);

        private void Verify(Times times) =>
            _mediator.Verify(x => x.Send(
                It.Is<UpdateSynchronizedObjectRequest>(r => r.SynchronizedObjectId.Id == SynchronizedLatency.SyncObjId.Id),
                It.IsAny<CancellationToken>()), times);

        [Fact]
        public async Task Report_FirstReport_StoreAndTellTheOthers()
        {
            var report = new LatencyReport(20, 40, 15);
            _repository.Setup(x => x.Set(_musician, report)).ReturnsAsync((LatencyReport?) null);

            await Create().Handle(new ReportLatencyRequest(_musician, report), default);

            _repository.Verify(x => x.Set(_musician, report), Times.Once);
            Verify(Times.Once());
        }

        [Fact]
        public async Task Report_ChangedALot_TellTheOthers()
        {
            _repository.Setup(x => x.Set(_musician, It.IsAny<LatencyReport>()))
                .ReturnsAsync(new LatencyReport(20, 40, 15));

            await Create().Handle(new ReportLatencyRequest(_musician, new LatencyReport(45, 40, 15)), default);

            Verify(Times.Once());
        }

        [Fact]
        public async Task Report_BarelyChanged_StoredButNotSentToEverybody()
        {
            _repository.Setup(x => x.Set(_musician, It.IsAny<LatencyReport>()))
                .ReturnsAsync(new LatencyReport(20, 40, 15));

            await Create().Handle(new ReportLatencyRequest(_musician, new LatencyReport(23, 42, 15)), default);

            _repository.Verify(x => x.Set(_musician, It.IsAny<LatencyReport>()), Times.Once);
            Verify(Times.Never());
        }

        [Theory]
        [InlineData(-1, 0, 0)]
        [InlineData(0, double.NaN, 0)]
        [InlineData(0, 0, double.PositiveInfinity)]
        [InlineData(6000, 0, 0)]
        public async Task Report_ImplausibleNumbers_Ignored(double rtt, double jitter, double output)
        {
            await Create().Handle(new ReportLatencyRequest(_musician, new LatencyReport(rtt, jitter, output)), default);

            _repository.Verify(x => x.Set(It.IsAny<Participant>(), It.IsAny<LatencyReport>()), Times.Never);
            Verify(Times.Never());
        }

        [Fact]
        public async Task Left_HadReport_RemoveItAndTellTheOthers()
        {
            _repository.Setup(x => x.Remove(_musician)).ReturnsAsync(true);
            var handler = new ParticipantLeftNotificationHandler(_repository.Object, _mediator.Object);

            await handler.Handle(new ParticipantLeftNotification(_musician, "connection"), default);

            Verify(Times.Once());
        }

        [Fact]
        public async Task Left_NeverReported_DoesNothing()
        {
            _repository.Setup(x => x.Remove(_musician)).ReturnsAsync(false);
            var handler = new ParticipantLeftNotificationHandler(_repository.Object, _mediator.Object);

            await handler.Handle(new ParticipantLeftNotification(_musician, "connection"), default);

            Verify(Times.Never());
        }
    }
}
