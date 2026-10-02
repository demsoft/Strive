using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediatR;
using Moq;
using Strive.Core.Services;
using Strive.Core.Services.Reactions;
using Strive.Core.Services.Reactions.Notifications;
using Strive.Core.Services.Reactions.Requests;
using Strive.Core.Services.Reactions.UseCases;
using Strive.Core.Services.Rooms.Gateways;
using Strive.Tests.Utils;
using Xunit;

namespace Strive.Core.Tests.Services.Reactions
{
    public class SendReactionUseCaseTests
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        private const string Emoji = "👍";

        private readonly Participant _participant = new("123", "participant");
        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<IRoomRepository> _roomRepository = new();
        private readonly Mock<IReactionRateLimiter> _rateLimiter = new();

        private class FixedTimeProvider : TimeProvider
        {
            public override DateTimeOffset GetUtcNow()
            {
                return Now;
            }
        }

        private SendReactionUseCase Create()
        {
            _rateLimiter.Setup(x => x.TryAcquire(_participant)).Returns(true);
            return new SendReactionUseCase(_mediator.Object, _roomRepository.Object, _rateLimiter.Object,
                new FixedTimeProvider());
        }

        [Fact]
        public async Task Handle_ValidReaction_NotifyParticipantsOfRoom()
        {
            // arrange
            var useCase = Create();
            var recipients = new List<Participant> {_participant, new("123", "other")};
            _roomRepository.Setup(x => x.GetRoomOfParticipant(_participant)).ReturnsAsync("room");
            _roomRepository.Setup(x => x.GetParticipantsOfRoom("123", "room")).ReturnsAsync(recipients);

            // act
            var result = await useCase.Handle(new SendReactionRequest(_participant, Emoji), default);

            // assert
            Assert.True(result.Success);
            _mediator.Verify(
                x => x.Publish(
                    It.Is<ParticipantReactedNotification>(n =>
                        n.ConferenceId == "123" && n.Sender.Equals(_participant) && n.Emoji == Emoji &&
                        n.Timestamp == Now && n.Recipients == recipients), default), Times.Once);
        }

        [Fact]
        public async Task Handle_EmojiNotAllowed_ReturnErrorAndDoNotNotify()
        {
            var useCase = Create();

            var result = await useCase.Handle(new SendReactionRequest(_participant, "💩"), default);

            Assert.False(result.Success);
            Assert.Equal(nameof(ServiceErrorCode.Reactions_InvalidEmoji), result.Error!.Code);
            _mediator.Verify(x => x.Publish(It.IsAny<ParticipantReactedNotification>(), default), Times.Never);
        }

        [Fact]
        public async Task Handle_RateLimited_ReturnErrorAndDoNotNotify()
        {
            var useCase = Create();
            _rateLimiter.Setup(x => x.TryAcquire(_participant)).Returns(false);

            var result = await useCase.Handle(new SendReactionRequest(_participant, Emoji), default);

            Assert.False(result.Success);
            Assert.Equal(nameof(ServiceErrorCode.Reactions_RateLimited), result.Error!.Code);
            _mediator.Verify(x => x.Publish(It.IsAny<ParticipantReactedNotification>(), default), Times.Never);
        }

        [Fact]
        public async Task Handle_ParticipantHasNoRoom_SucceedWithoutNotification()
        {
            var useCase = Create();
            _roomRepository.Setup(x => x.GetRoomOfParticipant(_participant)).ReturnsAsync((string?) null);

            var result = await useCase.Handle(new SendReactionRequest(_participant, Emoji), default);

            Assert.True(result.Success);
            _mediator.Verify(x => x.Publish(It.IsAny<ParticipantReactedNotification>(), default), Times.Never);
        }
    }
}
