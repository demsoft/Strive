using System;

namespace Strive.Hubs.Core.Responses
{
    public record ReactionDto(string ParticipantId, string Emoji, DateTimeOffset Timestamp);
}
