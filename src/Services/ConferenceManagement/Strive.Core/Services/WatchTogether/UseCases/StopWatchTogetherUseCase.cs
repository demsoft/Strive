using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.Synchronization.Requests;
using Strive.Core.Services.WatchTogether.Gateways;
using Strive.Core.Services.WatchTogether.Requests;

namespace Strive.Core.Services.WatchTogether.UseCases
{
    public class StopWatchTogetherUseCase : IRequestHandler<StopWatchTogetherRequest>
    {
        private readonly IMediator _mediator;
        private readonly IWatchTogetherRepository _repository;

        public StopWatchTogetherUseCase(IWatchTogetherRepository repository, IMediator mediator)
        {
            _repository = repository;
            _mediator = mediator;
        }

        public async Task Handle(StopWatchTogetherRequest request, CancellationToken cancellationToken)
        {
            if (!await _repository.Clear(request.ConferenceId)) return;

            await _mediator.Send(
                new UpdateSynchronizedObjectRequest(request.ConferenceId, SynchronizedWatchTogether.SyncObjId),
                cancellationToken);
        }
    }
}
