using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Strive.Core.Errors;
using Strive.Core.Services.ConferenceControl;
using Strive.Core.Services.ConferenceControl.Requests;
using Strive.Core.Services.Lobby.Notifications;
using Strive.Extensions;
using Strive.Hubs.Core.Responses;

namespace Strive.Hubs.Core.NotificationHandlers
{
    /// <summary>
    ///     Join the connection that waited in the lobby
    /// </summary>
    public class ParticipantAdmittedNotificationHandler : INotificationHandler<ParticipantAdmittedNotification>
    {
        private readonly IMediator _mediator;
        private readonly IHubContext<CoreHub> _hubContext;
        private readonly ICoreHubConnections _connections;
        private readonly ILogger<ParticipantAdmittedNotificationHandler> _logger;

        public ParticipantAdmittedNotificationHandler(IMediator mediator, IHubContext<CoreHub> hubContext,
            ICoreHubConnections connections, ILogger<ParticipantAdmittedNotificationHandler> logger)
        {
            _mediator = mediator;
            _hubContext = hubContext;
            _connections = connections;
            _logger = logger;
        }

        public async Task Handle(ParticipantAdmittedNotification notification, CancellationToken cancellationToken)
        {
            var (participant, connectionId, displayName) = notification;
            var client = _hubContext.Clients.Client(connectionId);

            try
            {
                await _mediator.Send(new JoinConferenceRequest(participant, connectionId,
                    new ParticipantMetadata(displayName)), cancellationToken);

                _connections.SetParticipant(participant.Id, new ParticipantConnection(participant.ConferenceId, connectionId));

                await client.LobbyStatus(new LobbyStatusDto(LobbyStatus.Admitted), cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Joining the admitted participant {participant} failed", participant);
                await client.SendAsync(CoreHubMessages.OnConnectionError, e.ToError(), cancellationToken);
            }
        }
    }
}
