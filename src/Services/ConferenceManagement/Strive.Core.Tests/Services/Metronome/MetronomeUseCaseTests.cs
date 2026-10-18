using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using Strive.Core.Services;
using Strive.Core.Services.Metronome;
using Strive.Core.Services.Metronome.Gateways;
using Strive.Core.Services.Metronome.Requests;
using Strive.Core.Services.Metronome.UseCases;
using Strive.Core.Services.Synchronization.Requests;
using Xunit;

namespace Strive.Core.Tests.Services.Metronome
{
    public class MetronomeUseCaseTests
    {
        private const string ConferenceId = "conference1";
        private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1700000000);

        private class FixedTime : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => Now;
        }

        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<IMetronomeRepository> _repository = new();
        private readonly Participant _teacher = new(ConferenceId, "teacher1");
        private MetronomeSession? _stored;

        public MetronomeUseCaseTests()
        {
            _repository.Setup(x => x.Set(ConferenceId, It.IsAny<MetronomeSession>()))
                .Callback<string, MetronomeSession>((_, s) => _stored = s).Returns(ValueTask.CompletedTask);
            _repository.Setup(x => x.Clear(ConferenceId)).Returns(() =>
            {
                var had = _stored != null;
                _stored = null;
                return new ValueTask<bool>(had);
            });
        }

        private StartMetronomeUseCase Start() => new(_mediator.Object, _repository.Object, new FixedTime());

        [Fact]
        public async Task Start_ValidTempo_StoreItAndSynchronize()
        {
            await Start().Handle(new StartMetronomeRequest(_teacher, 96, 4), default);

            Assert.Equal(96, _stored!.Bpm);
            Assert.Equal(4, _stored.BeatsPerBar);
            Assert.Equal("teacher1", _stored.StartedBy);
            _mediator.Verify(x => x.Send(
                It.Is<UpdateSynchronizedObjectRequest>(r => r.SynchronizedObjectId.Id == SynchronizedMetronome.SyncObjId.Id),
                It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task Start_FirstBeatIsALittleLater_EverybodyHasTheMessageBeforeIt()
        {
            await Start().Handle(new StartMetronomeRequest(_teacher, 120, 4), default);

            Assert.Equal(Now + MetronomeSession.LeadTime, _stored!.FirstBeatAt);
        }

        [Theory]
        [InlineData(29, 4)]
        [InlineData(301, 4)]
        [InlineData(120, 0)]
        [InlineData(120, 13)]
        public async Task Start_InvalidTempo_ThrowAndStoreNothing(int bpm, int beats)
        {
            await Assert.ThrowsAnyAsync<Exception>(() =>
                Start().Handle(new StartMetronomeRequest(_teacher, bpm, beats), default));

            Assert.Null(_stored);
        }

        [Theory]
        [InlineData(30, 1)]
        [InlineData(300, 12)]
        public void IsValid_Limits_Accepted(int bpm, int beats)
        {
            Assert.True(MetronomeSession.IsValid(bpm, beats));
        }

        [Fact]
        public async Task Start_WhileRunning_ReplacesTheTempo()
        {
            await Start().Handle(new StartMetronomeRequest(_teacher, 80, 3), default);
            await Start().Handle(new StartMetronomeRequest(_teacher, 140, 4), default);

            Assert.Equal(140, _stored!.Bpm);
        }

        [Fact]
        public async Task Stop_Running_ClearAndSynchronize()
        {
            _stored = new MetronomeSession(100, 4, "teacher1", Now);

            await new StopMetronomeUseCase(_mediator.Object, _repository.Object).Handle(
                new StopMetronomeRequest(ConferenceId), default);

            Assert.Null(_stored);
            _mediator.Verify(x => x.Send(It.IsAny<UpdateSynchronizedObjectRequest>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Stop_NothingRunning_DoesNothing()
        {
            await new StopMetronomeUseCase(_mediator.Object, _repository.Object).Handle(
                new StopMetronomeRequest(ConferenceId), default);

            _mediator.VerifyNoOtherCalls();
        }
    }
}
