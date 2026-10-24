using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Interfaces;
using Strive.Core.Services.Payments.Gateways;

namespace Strive.Core.Services.Social
{
    /// <summary>A person follows a creator. Doing it twice changes nothing.</summary>
    public record FollowCreatorRequest(string UserId, string CreatorId, string? UserName = null)
        : IRequest<SuccessOrError<Unit>>;

    /// <summary>A person stops following a creator. Doing it twice changes nothing.</summary>
    public record UnfollowCreatorRequest(string UserId, string CreatorId) : IRequest<SuccessOrError<Unit>>;

    public class FollowCreatorUseCase : IRequestHandler<FollowCreatorRequest, SuccessOrError<Unit>>
    {
        private readonly ICreatorRepo _creators;
        private readonly IFollowRepo _follows;
        private readonly TimeProvider _timeProvider;

        public FollowCreatorUseCase(ICreatorRepo creators, IFollowRepo follows, TimeProvider timeProvider)
        {
            _creators = creators;
            _follows = follows;
            _timeProvider = timeProvider;
        }

        public async Task<SuccessOrError<Unit>> Handle(FollowCreatorRequest request, CancellationToken cancellationToken)
        {
            var creator = await _creators.FindById(request.CreatorId);
            if (creator == null) return SocialError.CreatorNotFound;
            if (creator.UserId == request.UserId) return SocialError.OwnProfile;

            var name = CommentRules.CleanName(request.UserName);
            var added = await _follows.TryAdd(new Follow(Guid.NewGuid().ToString("N"), request.UserId, creator.CreatorId,
                _timeProvider.GetUtcNow()) {UserName = name});
            // following again keeps the name in the list of the creator current
            if (!added) await _follows.UpdateUserName(request.UserId, creator.CreatorId, name);
            return Unit.Value;
        }
    }

    public class UnfollowCreatorUseCase : IRequestHandler<UnfollowCreatorRequest, SuccessOrError<Unit>>
    {
        private readonly IFollowRepo _follows;

        public UnfollowCreatorUseCase(IFollowRepo follows)
        {
            _follows = follows;
        }

        public async Task<SuccessOrError<Unit>> Handle(UnfollowCreatorRequest request,
            CancellationToken cancellationToken)
        {
            await _follows.Remove(request.UserId, request.CreatorId);
            return Unit.Value;
        }
    }
}
