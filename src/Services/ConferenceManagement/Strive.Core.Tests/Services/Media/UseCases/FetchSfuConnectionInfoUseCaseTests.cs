using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Strive.Core.Services;
using Strive.Core.Services.Media;
using Strive.Core.Services.Media.Gateways;
using Strive.Core.Services.Media.Requests;
using Strive.Core.Services.Media.UseCases;
using Xunit;

namespace Strive.Core.Tests.Services.Media.UseCases
{
    public class FetchSfuConnectionInfoUseCaseTests
    {
        private readonly Mock<ISfuAuthTokenFactory> _tokenFactory = new();
        private readonly Mock<ITurnCredentialFactory> _turnCredentialFactory = new();

        private FetchSfuConnectionInfoUseCase Create(SfuConnectionOptions options)
        {
            return new(_tokenFactory.Object, new OptionsWrapper<SfuConnectionOptions>(options),
                _turnCredentialFactory.Object);
        }

        [Fact]
        public async Task Handle_FormatUrlAndReturnToken()
        {
            const string conferenceId = "123";
            const string connectionId = "connectionId";
            const string token = "testToken";

            // arrange
            var options = new SfuConnectionOptions("http://localhost/media/v1/{0}");
            var useCase = Create(options);

            var participant = new Participant(conferenceId, "participantId");

            _tokenFactory.Setup(x => x.GenerateToken(participant, connectionId)).ReturnsAsync(token);

            // act
            var result = await useCase.Handle(new FetchSfuConnectionInfoRequest(participant, connectionId),
                CancellationToken.None);

            // assert
            Assert.Equal(token, result.AuthToken);
            Assert.Equal($"http://localhost/media/v1/{conferenceId}", result.Url);
        }

        [Fact]
        public async Task Handle_TurnConfigured_ReturnIceServerOfParticipant()
        {
            // arrange
            var useCase = Create(new SfuConnectionOptions("http://localhost/media/v1/{0}"));
            var participant = new Participant("123", "participantId");
            var turnServer = new IceServerInfo(new[] {"turn:turn.example.com"}, "1700086400:participantId", "credential");

            _tokenFactory.Setup(x => x.GenerateToken(participant, "connectionId")).ReturnsAsync("testToken");
            _turnCredentialFactory.Setup(x => x.Create(participant)).Returns(turnServer);

            // act
            var result = await useCase.Handle(new FetchSfuConnectionInfoRequest(participant, "connectionId"),
                CancellationToken.None);

            // assert
            Assert.Equal(turnServer, Assert.Single(result.IceServers));
        }

        [Fact]
        public async Task Handle_TurnNotConfigured_ReturnNoIceServers()
        {
            // arrange
            var useCase = Create(new SfuConnectionOptions("http://localhost/media/v1/{0}"));
            var participant = new Participant("123", "participantId");

            _tokenFactory.Setup(x => x.GenerateToken(participant, "connectionId")).ReturnsAsync("testToken");
            _turnCredentialFactory.Setup(x => x.Create(participant)).Returns((IceServerInfo?) null);

            // act
            var result = await useCase.Handle(new FetchSfuConnectionInfoRequest(participant, "connectionId"),
                CancellationToken.None);

            // assert
            Assert.Empty(result.IceServers);
        }
    }
}
