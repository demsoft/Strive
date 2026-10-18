using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.Metronome.Gateways;
using Strive.Core.Services.Metronome.Requests;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.Metronome.UseCases
{
    public class StopMetronomeUseCase : IRequestHandler<StopMetronomeRequest>
    {
        private readonly IMediator _mediator;
        private readonly IMetronomeRepository _repository;

        public StopMetronomeUseCase(IMediator mediator, IMetronomeRepository repository)
        {
            _mediator = mediator;
            _repository = repository;
        }

        public async Task Handle(StopMetronomeRequest request, CancellationToken cancellationToken)
        {
            if (!await _repository.Clear(request.ConferenceId)) return;

            await _mediator.Send(
                new UpdateSynchronizedObjectRequest(request.ConferenceId, SynchronizedMetronome.SyncObjId),
                cancellationToken);
        }
    }
}
