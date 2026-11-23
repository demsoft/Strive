using System;

namespace Strive.Core.Domain.Entities
{
    public sealed class HrmMeetingOwnership
    {
        public string Organization { get; set; } = "";
        public string ExternalId { get; set; } = "";
        public DateTimeOffset EndsAt { get; set; }
        public string[] Participants { get; set; } = Array.Empty<string>();
    }
}
