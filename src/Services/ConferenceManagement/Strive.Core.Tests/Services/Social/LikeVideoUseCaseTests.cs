using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Strive.Core.Services;
using Strive.Core.Services.Social;
using Strive.Core.Services.Videos;
using Xunit;

namespace Strive.Core.Tests.Services.Social
{
    public class LikeVideoUseCaseTests
    {
        private static (LikeVideoUseCase useCase, Mock<IVideoLikeRepo> likes) Build(Video? video)
        {
            var videos = new Mock<IVideoRepo>();
            videos.Setup(x => x.FindById("v1")).ReturnsAsync(video);
            var likes = new Mock<IVideoLikeRepo>();
            likes.Setup(x => x.TryAdd(It.IsAny<VideoLike>())).ReturnsAsync(true);
            return (new LikeVideoUseCase(videos.Object, likes.Object, TimeProvider.System), likes);
        }

        private static Video Ready(VideoModeration moderation = VideoModeration.None) =>
            new("v1", "c1", "owner", "Reel", DateTimeOffset.UtcNow)
            {
                Status = VideoStatus.Ready, Moderation = moderation,
            };

        [Fact]
        public async Task Like_StoresTheLikeOfThePersonForTheVideo()
        {
            var (useCase, likes) = Build(Ready());

            var result = await useCase.Handle(new LikeVideoRequest("fan", "v1"), CancellationToken.None);

            Assert.True(result.Success);
            likes.Verify(x => x.TryAdd(It.Is<VideoLike>(l => l.UserId == "fan" && l.VideoId == "v1")), Times.Once);
        }

        [Fact]
        public async Task Like_Again_IsNotAnError()
        {
            var (useCase, likes) = Build(Ready());
            likes.Setup(x => x.TryAdd(It.IsAny<VideoLike>())).ReturnsAsync(false);

            Assert.True((await useCase.Handle(new LikeVideoRequest("fan", "v1"), CancellationToken.None)).Success);
        }

        [Theory]
        [InlineData(VideoModeration.Hidden)]
        [InlineData(VideoModeration.Removed)]
        public async Task Like_AVideoThatIsNotShown_NotFound(VideoModeration moderation)
        {
            var (useCase, likes) = Build(Ready(moderation));

            var result = await useCase.Handle(new LikeVideoRequest("fan", "v1"), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(nameof(ServiceErrorCode.Videos_NotFound), result.Error!.Code);
            likes.Verify(x => x.TryAdd(It.IsAny<VideoLike>()), Times.Never);
        }

        [Fact]
        public async Task Like_TheCreatorCanLikeTheirOwnVideo_EvenWhileItIsHidden()
        {
            var (useCase, likes) = Build(Ready(VideoModeration.Hidden));

            var result = await useCase.Handle(new LikeVideoRequest("owner", "v1"), CancellationToken.None);

            Assert.True(result.Success);
            likes.Verify(x => x.TryAdd(It.IsAny<VideoLike>()), Times.Once);
        }

        [Fact]
        public async Task Like_AVideoThatIsNotThere_NotFound()
        {
            var (useCase, _) = Build(null);

            var result = await useCase.Handle(new LikeVideoRequest("fan", "v1"), CancellationToken.None);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task Unlike_RemovesTheLike()
        {
            var likes = new Mock<IVideoLikeRepo>();

            var result = await new UnlikeVideoUseCase(likes.Object)
                .Handle(new UnlikeVideoRequest("fan", "v1"), CancellationToken.None);

            Assert.True(result.Success);
            likes.Verify(x => x.Remove("fan", "v1"), Times.Once);
        }
    }
}
