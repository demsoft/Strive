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
    public class StartWatchTogetherUseCase : IRequestHandler<StartWatchTogetherRequest>
    {
        private readonly IMediator _mediator;
        private readonly IWatchTogetherRepository _repository;
        private readonly TimeProvider _timeProvider;

        public StartWatchTogetherUseCase(IWatchTogetherRepository repository, IMediator mediator,
            TimeProvider timeProvider)
        {
            _repository = repository;
            _mediator = mediator;
            _timeProvider = timeProvider;
        }

        public async Task Handle(StartWatchTogetherRequest request, CancellationToken cancellationToken)
        {
            if (!YouTubeVideoUrl.TryParse(request.Url, out var videoId, out var startSeconds))
                throw WatchTogetherError.InvalidVideoUrl.ToException();

            var now = _timeProvider.GetUtcNow();
            // a video that was started before is replaced
            await _repository.Set(request.Participant.ConferenceId,
                new WatchTogetherSession(WatchTogetherSession.YouTube, videoId, request.Participant.Id, now,
                    WatchTogetherPlaybackState.Playing, startSeconds, 1, now));

            await _mediator.Send(
                new UpdateSynchronizedObjectRequest(request.Participant.ConferenceId,
                    SynchronizedWatchTogether.SyncObjId), cancellationToken);
        }
    }
}
