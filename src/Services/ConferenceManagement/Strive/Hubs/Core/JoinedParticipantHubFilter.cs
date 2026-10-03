using System;using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Strive.Infrastructure.Extensions;

namespace Strive.Hubs.Core
{
    /// <summary>
    ///     Only connections that actually joined the conference may call hub methods. Connections that wait in the lobby are
    ///     connected, but must not be able to do anything until they are admitted.
    /// </summary>
    public class JoinedParticipantHubFilter : IHubFilter
    {
        private readonly ICoreHubConnections _connections;

        public JoinedParticipantHubFilter(ICoreHubConnections connections)
        {
            _connections = connections;
        }

        public ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext,
            Func<HubInvocationContext, ValueTask<object?>> next)
        {
            // the equipment hub has its own authentication
            if (invocationContext.Hub is not CoreHub) return next(invocationContext);

            var participantId = invocationContext.Context.User?.GetUserId();

            if (participantId == null || !_connections.TryGetParticipant(participantId, out var connection) ||
                connection.ConnectionId != invocationContext.Context.ConnectionId)
                throw new HubException("The connection has not joined the conference.");

            return next(invocationContext);
        }
    }
}
