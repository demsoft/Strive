using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using Strive.Core.Services;
using Strive.Core.Services.Synchronization.Requests;
using Strive.Core.Services.WatchTogether;
using Strive.Core.Services.WatchTogether.Gateways;
using Strive.Core.Services.WatchTogether.Requests;
using Strive.Core.Services.WatchTogether.UseCases;
using Xunit;

namespace Strive.Core.Tests.Services.WatchTogether
{
    public class WatchTogetherUseCaseTests
    {
        private const string ConferenceId = "conference1";
        private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1700000000);

        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<IWatchTogetherRepository> _repository = new();
        private readonly Participant _moderator = new(ConferenceId, "moderator1");
        private DateTimeOffset _now = Start;
        private WatchTogetherSession? _stored;

        public WatchTogetherUseCaseTests()
        {
            _repository.Setup(x => x.Get(ConferenceId)).Returns(() => new ValueTask<WatchTogetherSession?>(_stored));
            _repository.Setup(x => x.Set(ConferenceId, It.IsAny<WatchTogetherSession>()))
                .Callback<string, WatchTogetherSession>((_, s) => _stored = s).Returns(ValueTask.CompletedTask);
            _repository.Setup(x => x.Clear(ConferenceId)).Returns(() =>
            {
                var had = _stored != null;
                _stored = null;
                return new ValueTask<bool>(had);
            });
        }

        private class TestTime : TimeProvider
        {
            private readonly Func<DateTimeOffset> _now;

            public TestTime(Func<DateTimeOffset> now)
            {
                _now = now;
            }

            public override DateTimeOffset GetUtcNow() => _now();
        }

        private TimeProvider Time => new TestTime(() => _now);

        private StartWatchTogetherUseCase StartUseCase => new(_repository.Object, _mediator.Object, Time);
        private ControlWatchTogetherUseCase ControlUseCase => new(_repository.Object, _mediator.Object, Time);
        private StopWatchTogetherUseCase StopUseCase => new(_repository.Object, _mediator.Object);

        private Task StartVideo(string url = "https://youtu.be/dQw4w9WgXcQ?t=10") =>
            StartUseCase.Handle(new StartWatchTogetherRequest(_moderator, url), CancellationToken.None);

        private Task Control(WatchTogetherAction action, double? position = null, double? rate = null) =>
            ControlUseCase.Handle(new ControlWatchTogetherRequest(ConferenceId, action, position, rate),
                CancellationToken.None);

        private void VerifySynchronized(Times times) => _mediator.Verify(
            x => x.Send(
                It.Is<UpdateSynchronizedObjectRequest>(r =>
                    r.ConferenceId == ConferenceId && r.SynchronizedObjectId.Equals(SynchronizedWatchTogether.SyncObjId)),
                It.IsAny<CancellationToken>()), times);

        [Fact]
        public async Task Start_ValidLink_PlaysFromTheStartTimeAndNotifiesEverybody()
        {
            await StartVideo();

            Assert.NotNull(_stored);
            Assert.Equal("youtube", _stored!.Provider);
            Assert.Equal("dQw4w9WgXcQ", _stored.VideoId);
            Assert.Equal("moderator1", _stored.StartedBy);
            Assert.Equal(WatchTogetherPlaybackState.Playing, _stored.State);
            Assert.Equal(10, _stored.PositionSeconds);
            Assert.Equal(1, _stored.Rate);
            Assert.Equal(Start, _stored.UpdatedAt);
            VerifySynchronized(Times.Once());
        }

        [Fact]
        public async Task Start_InvalidLink_Throws_AndChangesNothing()
        {
            var error = await Assert.ThrowsAsync<IdErrorException>(() => StartVideo("https://evil.com/watch?v=dQw4w9WgXcQ"));

            Assert.Equal(nameof(ServiceErrorCode.WatchTogether_InvalidVideoUrl), error.Error.Code);
            Assert.Null(_stored);
            VerifySynchronized(Times.Never());
        }

        [Fact]
        public async Task Start_AnotherVideoWhileOneIsPlaying_ReplacesIt()
        {
            await StartVideo("https://youtu.be/aaaaaaaaaaa");
            await StartVideo("https://youtu.be/bbbbbbbbbbb");

            Assert.Equal("bbbbbbbbbbb", _stored!.VideoId);
        }

        [Fact]
        public async Task Control_PauseAndPlay_KeepThePositionOfTheHost()
        {
            await StartVideo("https://youtu.be/dQw4w9WgXcQ");
            _now = Start.AddSeconds(30);

            await Control(WatchTogetherAction.Pause, 31.5);
            Assert.Equal(WatchTogetherPlaybackState.Paused, _stored!.State);
            Assert.Equal(31.5, _stored.PositionSeconds);
            Assert.Equal(_now, _stored.UpdatedAt);

            _now = Start.AddSeconds(60);
            await Control(WatchTogetherAction.Play, 31.5);
            Assert.Equal(WatchTogetherPlaybackState.Playing, _stored.State);
            Assert.Equal(31.5, _stored.PositionSeconds);
            Assert.Equal(_now, _stored.UpdatedAt);
        }

        [Fact]
        public async Task Control_Seek_KeepsThePlaybackState()
        {
            await StartVideo("https://youtu.be/dQw4w9WgXcQ");
            await Control(WatchTogetherAction.Pause, 5);

            await Control(WatchTogetherAction.Seek, 120);

            Assert.Equal(WatchTogetherPlaybackState.Paused, _stored!.State);
            Assert.Equal(120, _stored.PositionSeconds);
        }

        [Fact]
        public async Task Control_Rate_ContinuesFromWhereTheVideoIsNow()
        {
            await StartVideo("https://youtu.be/dQw4w9WgXcQ?t=100");
            _now = Start.AddSeconds(20);

            await Control(WatchTogetherAction.SetRate, rate: 1.5);

            Assert.Equal(1.5, _stored!.Rate);
            Assert.Equal(120, _stored.PositionSeconds);
            Assert.Equal(_now, _stored.UpdatedAt);
        }

        [Fact]
        public async Task PositionAt_PlayingFasterOrPaused()
        {
            await StartVideo("https://youtu.be/dQw4w9WgXcQ?t=100");
            await Control(WatchTogetherAction.SetRate, rate: 2);

            Assert.Equal(110, _stored!.PositionAt(_now.AddSeconds(5)));

            await Control(WatchTogetherAction.Pause, 100);
            Assert.Equal(100, _stored!.PositionAt(_now.AddSeconds(500)));
        }

        [Fact]
        public async Task Control_NothingPlaying_Throws()
        {
            var error = await Assert.ThrowsAsync<IdErrorException>(() => Control(WatchTogetherAction.Pause, 1));

            Assert.Equal(nameof(ServiceErrorCode.WatchTogether_NothingPlaying), error.Error.Code);
            VerifySynchronized(Times.Never());
        }

        [Theory]
        [InlineData(WatchTogetherAction.Play, null, null)]
        [InlineData(WatchTogetherAction.Seek, -1.0, null)]
        [InlineData(WatchTogetherAction.Seek, double.NaN, null)]
        [InlineData(WatchTogetherAction.Seek, double.PositiveInfinity, null)]
        [InlineData(WatchTogetherAction.Seek, 90000.0, null)]
        [InlineData(WatchTogetherAction.SetRate, null, null)]
        [InlineData(WatchTogetherAction.SetRate, null, 0.1)]
        [InlineData(WatchTogetherAction.SetRate, null, 5.0)]
        [InlineData(WatchTogetherAction.SetRate, null, double.NaN)]
        public async Task Control_InvalidValues_Throw_AndChangeNothing(WatchTogetherAction action, double? position,
            double? rate)
        {
            await StartVideo("https://youtu.be/dQw4w9WgXcQ");
            var before = _stored;
            _mediator.Invocations.Clear();

            var error = await Assert.ThrowsAsync<IdErrorException>(() => Control(action, position, rate));

            Assert.Equal(nameof(ServiceErrorCode.WatchTogether_InvalidControl), error.Error.Code);
            Assert.Equal(before, _stored);
            VerifySynchronized(Times.Never());
        }

        [Fact]
        public async Task Stop_RemovesTheVideoAndNotifies_OnlyOnce()
        {
            await StartVideo();
            _mediator.Invocations.Clear();

            await StopUseCase.Handle(new StopWatchTogetherRequest(ConferenceId), CancellationToken.None);
            await StopUseCase.Handle(new StopWatchTogetherRequest(ConferenceId), CancellationToken.None);

            Assert.Null(_stored);
            VerifySynchronized(Times.Once());
        }
    }
}
