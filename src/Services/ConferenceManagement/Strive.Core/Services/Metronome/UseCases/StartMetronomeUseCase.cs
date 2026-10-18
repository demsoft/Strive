using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Extensions;
using Strive.Core.Services.Metronome.Gateways;
using Strive.Core.Services.Metronome.Requests;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.Metronome.UseCases
{
    public class StartMetronomeUseCase : IRequestHandler<StartMetronomeRequest>
    {
        private readonly IMediator _mediator;
        private readonly IMetronomeRepository _repository;
        private readonly TimeProvider _timeProvider;

        public StartMetronomeUseCase(IMediator mediator, IMetronomeRepository repository, TimeProvider timeProvider)
        {
            _mediator = mediator;
            _repository = repository;
            _timeProvider = timeProvider;
        }

        public async Task Handle(StartMetronomeRequest request, CancellationToken cancellationToken)
        {
            var (participant, bpm, beatsPerBar) = request;
            if (!MetronomeSession.IsValid(bpm, beatsPerBar)) throw MetronomeError.InvalidTempo.ToException();

            var firstBeat = _timeProvider.GetUtcNow().Add(MetronomeSession.LeadTime);
            await _repository.Set(participant.ConferenceId,
                new MetronomeSession(bpm, beatsPerBar, participant.Id, firstBeat));

            await _mediator.Send(
                new UpdateSynchronizedObjectRequest(participant.ConferenceId, SynchronizedMetronome.SyncObjId),
                cancellationToken);
        }
    }
}
