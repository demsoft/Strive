using System;
using System.Collections.Generic;
using MediatR;

namespace Strive.Core.Services.Reactions.Notifications
{
    /// <summary>
    ///     A participant sent a reaction that is shown to <paramref name="Recipients" /> (the participants in the room of the sender)
    /// </summary>
    public record ParticipantReactedNotification(string ConferenceId, IReadOnlyList<Participant> Recipients,
        Participant Sender, string Emoji, DateTimeOffset Timestamp) : INotification;
}
