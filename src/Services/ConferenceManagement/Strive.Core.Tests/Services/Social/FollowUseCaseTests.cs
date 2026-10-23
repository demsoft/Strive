using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Strive.Core.Services;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Social;
using Xunit;

namespace Strive.Core.Tests.Services.Social
{
    public class FollowUseCaseTests
    {
        private static (FollowCreatorUseCase useCase, Mock<IFollowRepo> follows) Build(bool creatorExists = true)
        {
            var creators = new Mock<ICreatorRepo>();
            if (creatorExists)
                creators.Setup(x => x.FindById("c1"))
                    .ReturnsAsync(new CreatorProfile("c1", "owner", "Ada", DateTimeOffset.UtcNow));
            var follows = new Mock<IFollowRepo>();
            follows.Setup(x => x.TryAdd(It.IsAny<Follow>())).ReturnsAsync(true);
            return (new FollowCreatorUseCase(creators.Object, follows.Object, TimeProvider.System), follows);
        }

        [Fact]
        public async Task Follow_StoresTheFollowOfThePersonForTheCreator()
        {
            var (useCase, follows) = Build();

            var result = await useCase.Handle(new FollowCreatorRequest("fan", "c1"), CancellationToken.None);

            Assert.True(result.Success);
            follows.Verify(x => x.TryAdd(It.Is<Follow>(f => f.UserId == "fan" && f.CreatorId == "c1")), Times.Once);
        }

        [Fact]
        public async Task Follow_AgainIsNotAnError()
        {
            var (useCase, follows) = Build();
            follows.Setup(x => x.TryAdd(It.IsAny<Follow>())).ReturnsAsync(false);

            var result = await useCase.Handle(new FollowCreatorRequest("fan", "c1"), CancellationToken.None);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task Follow_ACreatorThatIsNotThere_NotFound()
        {
            var (useCase, follows) = Build(creatorExists: false);

            var result = await useCase.Handle(new FollowCreatorRequest("fan", "c1"), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(nameof(ServiceErrorCode.Social_NotFound), result.Error!.Code);
            follows.Verify(x => x.TryAdd(It.IsAny<Follow>()), Times.Never);
        }

        [Fact]
        public async Task Follow_Yourself_IsRefused()
        {
            var (useCase, follows) = Build();

            var result = await useCase.Handle(new FollowCreatorRequest("owner", "c1"), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(nameof(ServiceErrorCode.Social_Invalid), result.Error!.Code);
            follows.Verify(x => x.TryAdd(It.IsAny<Follow>()), Times.Never);
        }

        [Fact]
        public async Task Unfollow_RemovesTheFollow_EvenOfACreatorThatIsGone()
        {
            var follows = new Mock<IFollowRepo>();

            var result = await new UnfollowCreatorUseCase(follows.Object)
                .Handle(new UnfollowCreatorRequest("fan", "gone"), CancellationToken.None);

            Assert.True(result.Success);
            follows.Verify(x => x.Remove("fan", "gone"), Times.Once);
        }
    }
}
