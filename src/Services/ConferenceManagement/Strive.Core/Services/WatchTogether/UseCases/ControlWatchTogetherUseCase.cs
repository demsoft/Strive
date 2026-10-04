using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Extensions;
using Strive.Core.Services.Synchronization.Requests;
using Strive.Core.Services.WatchTogether.Gateways;
using Strive.Core.Services.WatchTogether.Requests;

namespace Strive.Core.Services.WatchTogether.UseCases
{
    public class ControlWatchTogetherUseCase : IRequestHandler<ControlWatchTogetherRequest>
    {
        public const double MaxPositionSeconds = 24 * 60 * 60;
        public const double MinRate = 0.25;
        public const double MaxRate = 2;

        private readonly IMediator _mediator;
        private readonly IWatchTogetherRepository _repository;
        private readonly TimeProvider _timeProvider;

        public ControlWatchTogetherUseCase(IWatchTogetherRepository repository, IMediator mediator,
            TimeProvider timeProvider)
        {
            _repository = repository;
            _mediator = mediator;
            _timeProvider = timeProvider;
        }

        public async Task Handle(ControlWatchTogetherRequest request, CancellationToken cancellationToken)
        {
            var session = await _repository.Get(request.ConferenceId);
            if (session == null) throw WatchTogetherError.NothingPlaying.ToException();

            var now = _timeProvider.GetUtcNow();
            var updated = request.Action switch
            {
                WatchTogetherAction.Play => session with
                {
                    State = WatchTogetherPlaybackState.Playing, PositionSeconds = ValidPosition(request), UpdatedAt = now,
                },
                WatchTogetherAction.Pause => session with
                {
                    State = WatchTogetherPlaybackState.Paused, PositionSeconds = ValidPosition(request), UpdatedAt = now,
                },
                WatchTogetherAction.Seek => session with {PositionSeconds = ValidPosition(request), UpdatedAt = now},
                // the position continues from where the video is now, only the speed changes
                WatchTogetherAction.SetRate => session with
                {
                    Rate = ValidRate(request), PositionSeconds = session.PositionAt(now), UpdatedAt = now,
                },
                _ => throw WatchTogetherError.InvalidControl.ToException(),
            };

            await _repository.Set(request.ConferenceId, updated);
            await _mediator.Send(
                new UpdateSynchronizedObjectRequest(request.ConferenceId, SynchronizedWatchTogether.SyncObjId),
                cancellationToken);
        }

        private static double ValidPosition(ControlWatchTogetherRequest request)
        {
            var position = request.PositionSeconds;
            if (position is not { } value || double.IsNaN(value) || double.IsInfinity(value) || value < 0 ||
                value > MaxPositionSeconds)
                throw WatchTogetherError.InvalidControl.ToException();

            return value;
        }

        private static double ValidRate(ControlWatchTogetherRequest request)
        {
            var rate = request.Rate;
            if (rate is not { } value || double.IsNaN(value) || value < MinRate || value > MaxRate)
                throw WatchTogetherError.InvalidControl.ToException();

            return value;
        }
    }
}
