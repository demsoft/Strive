using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Strive.Core.Services;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Social;
using Strive.Core.Services.Videos;
using Xunit;

namespace Strive.Core.Tests.Services.Social
{
    public class CommentUseCaseTests
    {
        private static Video Ready(long price = 0) =>
            new("v1", "c1", "owner", "Reel", DateTimeOffset.UtcNow)
            {
                Status = VideoStatus.Ready, PriceMinor = price,
            };

        private static (PostCommentUseCase useCase, Mock<IVideoCommentRepo> comments) BuildPost(Video? video,
            long recentComments = 0, bool holdsVideo = false)
        {
            var videos = new Mock<IVideoRepo>();
            videos.Setup(x => x.FindById("v1")).ReturnsAsync(video);
            var comments = new Mock<IVideoCommentRepo>();
            comments.Setup(x => x.CountSince(It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
                .ReturnsAsync(recentComments);
            var entitlements = new Mock<IVideoEntitlementRepo>();
            entitlements.Setup(x => x.HeldAmong(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(holdsVideo ? new[] {"v1"} : Array.Empty<string>());
            var access = new VideoAccess(entitlements.Object, new Mock<IPassRepo>().Object, TimeProvider.System);
            return (new PostCommentUseCase(videos.Object, comments.Object, access, TimeProvider.System), comments);
        }

        private static Task<Strive.Core.Interfaces.SuccessOrError<VideoComment>> Post(PostCommentUseCase useCase,
            string? text = "Lovely!", string user = "fan", string name = "Tola") =>
            useCase.Handle(new PostCommentRequest(user, name, "v1", text), CancellationToken.None);

        [Fact]
        public async Task Post_StoresTheTrimmedTextWithTheNameOfTheAuthor()
        {
            var (useCase, comments) = BuildPost(Ready());

            var result = await Post(useCase, "  Lovely!  ");

            Assert.True(result.Success);
            Assert.Equal("Lovely!", result.Response!.Text);
            Assert.Equal("Tola", result.Response.AuthorName);
            comments.Verify(x => x.Add(It.Is<VideoComment>(c => c.VideoId == "v1" && c.UserId == "fan")), Times.Once);
        }

        [Theory]
        [InlineData(null, "Someone")]
        [InlineData("   ", "Someone")]
        [InlineData("  Tola  ", "Tola")]
        public async Task Post_CleansTheName(string? name, string expected)
        {
            var (useCase, _) = BuildPost(Ready());

            var result = await Post(useCase, name: name!);

            Assert.Equal(expected, result.Response!.AuthorName);
        }

        [Fact]
        public async Task Post_ALongNameIsCut()
        {
            var (useCase, _) = BuildPost(Ready());

            var result = await Post(useCase, name: new string('x', 200));

            Assert.Equal(CommentRules.MaxNameLength, result.Response!.AuthorName.Length);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Post_NothingWritten_IsRefused(string? text)
        {
            var (useCase, comments) = BuildPost(Ready());

            var result = await Post(useCase, text);

            Assert.False(result.Success);
            Assert.Equal(nameof(ServiceErrorCode.Social_InvalidComment), result.Error!.Code);
            comments.Verify(x => x.Add(It.IsAny<VideoComment>()), Times.Never);
        }

        [Fact]
        public async Task Post_TooLong_IsRefused_AtTheLimitItIsFine()
        {
            var (useCase, _) = BuildPost(Ready());

            Assert.True((await Post(useCase, new string('x', CommentRules.MaxLength))).Success);
            var result = await Post(useCase, new string('x', CommentRules.MaxLength + 1));

            Assert.Equal(nameof(ServiceErrorCode.Social_InvalidComment), result.Error!.Code);
        }

        [Fact]
        public async Task Post_AVideoThatIsNotShown_NotFound()
        {
            Assert.False((await Post(BuildPost(null).useCase)).Success);
            var hidden = Ready();
            hidden.Moderation = VideoModeration.Hidden;
            var result = await Post(BuildPost(hidden).useCase);

            Assert.Equal(nameof(ServiceErrorCode.Videos_NotFound), result.Error!.Code);
        }

        [Fact]
        public async Task Post_UnderAPaidVideo_NeedsTheRightToWatchIt()
        {
            var (locked, _) = BuildPost(Ready(250000));
            var (bought, _) = BuildPost(Ready(250000), holdsVideo: true);
            var (own, _) = BuildPost(Ready(250000));

            var refused = await Post(locked);
            Assert.Equal(nameof(ServiceErrorCode.Social_CannotComment), refused.Error!.Code);
            Assert.True((await Post(bought)).Success);
            Assert.True((await Post(own, user: "owner")).Success);
        }

        [Fact]
        public async Task Post_TooManyInAMinute_IsRefused()
        {
            var (ok, _) = BuildPost(Ready(), CommentRules.MaxPerMinute - 1);
            var (flood, comments) = BuildPost(Ready(), CommentRules.MaxPerMinute);

            Assert.True((await Post(ok)).Success);
            var result = await Post(flood);

            Assert.Equal(nameof(ServiceErrorCode.Social_TooFast), result.Error!.Code);
            comments.Verify(x => x.Add(It.IsAny<VideoComment>()), Times.Never);
        }

        // ---- deleting

        private static (DeleteCommentUseCase useCase, Mock<IVideoCommentRepo> comments) BuildDelete(
            string writer = "fan")
        {
            var videos = new Mock<IVideoRepo>();
            videos.Setup(x => x.FindById("v1")).ReturnsAsync(Ready());
            var comments = new Mock<IVideoCommentRepo>();
            comments.Setup(x => x.FindById("k1"))
                .ReturnsAsync(new VideoComment("k1", "v1", writer, "Tola", "Hi", DateTimeOffset.UtcNow));
            return (new DeleteCommentUseCase(videos.Object, comments.Object), comments);
        }

        [Theory]
        [InlineData("fan", false)]
        [InlineData("owner", false)]
        [InlineData("moderator", true)]
        public async Task Delete_TheWriter_TheCreatorOfTheVideo_AndAnAdministratorCan(string user, bool admin)
        {
            var (useCase, comments) = BuildDelete();

            var result = await useCase.Handle(new DeleteCommentRequest(user, admin, "v1", "k1"),
                CancellationToken.None);

            Assert.True(result.Success);
            comments.Verify(x => x.Delete("k1"), Times.Once);
        }

        [Fact]
        public async Task Delete_SomebodyElse_NotFound_AndItStays()
        {
            var (useCase, comments) = BuildDelete();

            var result = await useCase.Handle(new DeleteCommentRequest("stranger", false, "v1", "k1"),
                CancellationToken.None);

            Assert.Equal(nameof(ServiceErrorCode.Social_CommentNotFound), result.Error!.Code);
            comments.Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Delete_ACommentOfAnotherVideo_OrOneThatIsGone_NotFound()
        {
            var (useCase, comments) = BuildDelete();

            Assert.False((await useCase.Handle(new DeleteCommentRequest("fan", false, "other", "k1"),
                CancellationToken.None)).Success);
            Assert.False((await useCase.Handle(new DeleteCommentRequest("fan", false, "v1", "nothing"),
                CancellationToken.None)).Success);
            comments.Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
        }
    }
}
