using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Strive.Core.Services.Videos;
using Xunit;

namespace Strive.Core.Tests.Services.Videos
{
    public class ReportVideoUseCaseTests
    {
        private static Video ReadyVideo() =>
            new("v1", "c1", "owner", "Reel", DateTimeOffset.UtcNow) {Status = VideoStatus.Ready};

        private static (ReportVideoUseCase useCase, Mock<IVideoRepo> videos, Mock<IVideoReportRepo> reports) Build(
            int reportsToHide, int openAfterAdding, bool added = true, Video? video = null)
        {
            var videos = new Mock<IVideoRepo>();
            videos.Setup(x => x.FindById("v1")).ReturnsAsync(video ?? ReadyVideo());
            var reports = new Mock<IVideoReportRepo>();
            reports.Setup(x => x.TryAdd(It.IsAny<VideoReport>())).ReturnsAsync(added);
            reports.Setup(x => x.CountOpen("v1")).ReturnsAsync(openAfterAdding);
            var useCase = new ReportVideoUseCase(videos.Object, reports.Object,
                Options.Create(new VideoOptions {ReportsToHide = reportsToHide}), TimeProvider.System,
                NullLogger<ReportVideoUseCase>.Instance);
            return (useCase, videos, reports);
        }

        private static Task<Strive.Core.Interfaces.SuccessOrError<MediatR.Unit>> Send(ReportVideoUseCase useCase,
            string user = "fan", ReportReason reason = ReportReason.Spam) =>
            useCase.Handle(new ReportVideoRequest(user, "v1", reason, null), CancellationToken.None);

        [Fact]
        public async Task TheNumberOfReportsThatHidesAVideoIsASetting()
        {
            var (below, belowVideos, _) = Build(reportsToHide: 5, openAfterAdding: 4);
            var (at, atVideos, _) = Build(reportsToHide: 5, openAfterAdding: 5);

            await Send(below);
            await Send(at);

            belowVideos.Verify(x => x.Update(It.IsAny<Video>()), Times.Never);
            atVideos.Verify(x => x.Update(It.Is<Video>(v => v.Moderation == VideoModeration.Hidden)), Times.Once);
        }

        [Fact]
        public async Task ReportingAgain_ChangesNothing_NotEvenTheCount()
        {
            var (useCase, videos, reports) = Build(reportsToHide: 1, openAfterAdding: 9, added: false);

            var result = await Send(useCase);

            Assert.True(result.Success);
            reports.Verify(x => x.CountOpen(It.IsAny<string>()), Times.Never);
            videos.Verify(x => x.Update(It.IsAny<Video>()), Times.Never);
        }

        [Fact]
        public async Task AVideoThatIsAlreadyHidden_IsNotReported()
        {
            var hidden = ReadyVideo();
            hidden.Moderation = VideoModeration.Hidden;
            var (useCase, _, reports) = Build(1, 1, video: hidden);

            var result = await Send(useCase);

            Assert.False(result.Success);
            reports.Verify(x => x.TryAdd(It.IsAny<VideoReport>()), Times.Never);
        }

        [Fact]
        public async Task ASettingOfZeroOrLess_StillNeedsAtLeastOneReport()
        {
            var (useCase, videos, _) = Build(reportsToHide: 0, openAfterAdding: 1);

            await Send(useCase);

            videos.Verify(x => x.Update(It.Is<Video>(v => v.Moderation == VideoModeration.Hidden)), Times.Once);
        }

        [Fact]
        public void IsPublic_OnlyAReadyVideoThatIsNotModerated()
        {
            var video = ReadyVideo();
            Assert.True(video.IsPublic);

            video.Moderation = VideoModeration.Hidden;
            Assert.False(video.IsPublic);
            video.Moderation = VideoModeration.Removed;
            Assert.False(video.IsPublic);

            video.Moderation = VideoModeration.None;
            video.Status = VideoStatus.Processing;
            Assert.False(video.IsPublic);
        }
    }
}
