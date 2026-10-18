using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.Latency.Gateways;
using Strive.Core.Services.Latency.Requests;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.Latency.UseCases
{
    public class ReportLatencyUseCase : IRequestHandler<ReportLatencyRequest>
    {
        /// <summary>Smaller changes are stored but not sent to everybody: the numbers move all the time</summary>
        public const double WorthTellingMs = 10;

        private readonly IMediator _mediator;
        private readonly ILatencyRepository _repository;

        public ReportLatencyUseCase(IMediator mediator, ILatencyRepository repository)
        {
            _mediator = mediator;
            _repository = repository;
        }

        public async Task Handle(ReportLatencyRequest request, CancellationToken cancellationToken)
        {
            var (participant, report) = request;
            // numbers from a browser: anything that is not plausible is ignored
            if (!report.IsValid()) return;

            var before = await _repository.Set(participant, report);
            if (before != null && Math.Abs(before.Total - report.Total) < WorthTellingMs) return;

            await _mediator.Send(new UpdateSynchronizedObjectRequest(participant.ConferenceId,
                SynchronizedLatency.SyncObjId), cancellationToken);
        }
    }
}
