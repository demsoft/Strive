using System;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Strive.Core.Interfaces;
using Strive.Core.Services;
using Strive.Core.Services.Reactions;
using Strive.Hubs.Core;
using Strive.Hubs.Core.Dtos;
using Strive.Hubs.Core.Responses;
using Strive.IntegrationTests._Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    [Collection(IntegrationTestCollection.Definition)]
    public class ReactionTests : ServiceIntegrationTest
    {
        public ReactionTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper,
            mongoDb)
        {
        }

        private static TaskCompletionSource<ReactionDto> ListenForReaction(UserConnection connection)
        {
            var tcs = new TaskCompletionSource<ReactionDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.Hub.On<ReactionDto>(CoreHubMessages.OnReaction, dto => tcs.TrySetResult(dto));
            return tcs;
        }

        private static Task<SuccessOrError<Unit>> Send(UserConnection connection, string emoji)
        {
            return connection.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.SendReaction),
                new SendReactionDto(emoji));
        }

        private static async Task<ReactionDto> Receive(TaskCompletionSource<ReactionDto> tcs)
        {
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.Same(tcs.Task, completed);
            return await tcs.Task;
        }

        [Fact]
        public async Task SendReaction_ParticipantsInSameRoom_AllReceiveReaction()
        {
            // arrange
            var (moderator, conference) = await ConnectToOpenedConference();
            var user = CreateUser();
            var userConnection = await ConnectUserToConference(user, conference);
            var moderatorReceived = ListenForReaction(moderator);
            var userReceived = ListenForReaction(userConnection);

            // act
            var result = await Send(userConnection, Reaction.AllowedEmojis[0]);

            // assert
            AssertSuccess(result);
            foreach (var received in new[] {moderatorReceived, userReceived})
            {
                var reaction = await Receive(received);
                Assert.Equal(user.Sub, reaction.ParticipantId);
                Assert.Equal(Reaction.AllowedEmojis[0], reaction.Emoji);
            }
        }

        [Fact]
        public async Task SendReaction_EmojiNotAllowed_ReturnValidationError()
        {
            var (connection, _) = await ConnectToOpenedConference();

            var result = await Send(connection, "💩");

            AssertFailed(result);
        }

        [Fact]
        public async Task SendReaction_ConferenceNotOpen_ReturnError()
        {
            var conference = await CreateConference(Moderator);
            var connection = await ConnectUserToConference(Moderator, conference);

            var result = await Send(connection, Reaction.AllowedEmojis[0]);

            AssertFailed(result);
        }

        [Fact]
        public async Task SendReaction_TooManyReactions_ReturnRateLimited()
        {
            // arrange
            var (connection, _) = await ConnectToOpenedConference();
            for (var i = 0; i < ReactionRateLimiter.MaxReactions; i++)
                AssertSuccess(await Send(connection, Reaction.AllowedEmojis[0]));

            // act
            var result = await Send(connection, Reaction.AllowedEmojis[0]);

            // assert
            AssertFailed(result);
            AssertErrorCode(ServiceErrorCode.Reactions_RateLimited, result.Error!);
        }
    }
}
