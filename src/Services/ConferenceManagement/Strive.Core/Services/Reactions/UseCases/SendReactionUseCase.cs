using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Interfaces;
using Strive.Core.Services.Reactions.Notifications;
using Strive.Core.Services.Reactions.Requests;
using Strive.Core.Services.Rooms.Gateways;

namespace Strive.Core.Services.Reactions.UseCases
{
    public class SendReactionUseCase : IRequestHandler<SendReactionRequest, SuccessOrError<Unit>>
    {
        private readonly IMediator _mediator;
        private readonly IRoomRepository _roomRepository;
        private readonly IReactionRateLimiter _rateLimiter;
        private readonly TimeProvider _timeProvider;

        public SendReactionUseCase(IMediator mediator, IRoomRepository roomRepository,
            IReactionRateLimiter rateLimiter, TimeProvider timeProvider)
        {
            _mediator = mediator;
            _roomRepository = roomRepository;
            _rateLimiter = rateLimiter;
            _timeProvider = timeProvider;
        }

        public async Task<SuccessOrError<Unit>> Handle(SendReactionRequest request,
            CancellationToken cancellationToken)
        {
            var (participant, emoji) = request;

            if (!Reaction.IsAllowed(emoji)) return ReactionError.InvalidEmoji;
            if (!_rateLimiter.TryAcquire(participant)) return ReactionError.RateLimited;

            var roomId = await _roomRepository.GetRoomOfParticipant(participant);
            if (roomId == null) return Unit.Value;

            var recipients = await _roomRepository.GetParticipantsOfRoom(participant.ConferenceId, roomId);
            await _mediator.Publish(
                new ParticipantReactedNotification(participant.ConferenceId, recipients, participant, emoji,
                    _timeProvider.GetUtcNow()), cancellationToken);

            return Unit.Value;
        }
    }
}
