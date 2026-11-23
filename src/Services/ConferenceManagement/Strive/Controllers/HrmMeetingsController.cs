using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Strive.Core.Domain.Entities;
using Strive.Core.Services.ConferenceManagement.Gateways;
using Strive.Core.Services.Permissions;
using Strive.Core.Services.Permissions.Options;
using Strive.Hrm;

namespace Strive.Controllers
{
    [ApiController]
    [Route("v1/hrm/meetings")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public sealed class HrmMeetingsController : ControllerBase
    {
        private readonly HrmMeetingBridge bridge;
        private readonly HrmMeetingOptions options;
        private readonly IConferenceRepo conferences;
        private readonly DefaultPermissionOptions permissions;
        public HrmMeetingsController(HrmMeetingBridge bridge, HrmMeetingOptions options, IConferenceRepo conferences, IOptions<DefaultPermissionOptions> permissions)
        { this.bridge = bridge; this.options = options; this.conferences = conferences; this.permissions = permissions.Value; }
        private bool Service => bridge.IsService(Request.Headers["X-GOS-HRM-Service"].ToString());
        public sealed class Provision
        {
            [Required, MaxLength(128)] public string Organization { get; set; } = "";
            [Required, MaxLength(64)] public string ExternalId { get; set; } = "";
            [Required, MaxLength(100)] public string Organizer { get; set; } = "";
            [Required, MaxLength(150)] public string Title { get; set; } = "";
            public DateTimeOffset StartsAt { get; set; }
            public DateTimeOffset EndsAt { get; set; }
            [Required, MinLength(1), MaxLength(50)] public string[] Participants { get; set; } = Array.Empty<string>();
        }
        public sealed class Join
        {
            [Required, MaxLength(128)] public string Organization { get; set; } = "";
            [Required, MaxLength(100)] public string Employee { get; set; } = "";
            [Required, MaxLength(150)] public string Name { get; set; } = "";
            public DateTimeOffset AuthorizedUntil { get; set; }
            [MaxLength(250)] public string? ParentOrigin { get; set; }
        }
        public sealed class Redemption { [Required, MaxLength(64)] public string Ticket { get; set; } = ""; }
        [HttpGet("bootstrap")]
        public IActionResult Bootstrap() => Ok(new { enabled = bridge.Ready,
            parentOrigin = bridge.Ready ? options.PortalOrigin : "", meetingOrigin = bridge.Ready ? options.MeetingOrigin : "" });
        [HttpGet("{id}/bootstrap")]
        public async Task<IActionResult> BootstrapMeeting(string id, [FromQuery] string? parentOrigin = null)
        {
            if (!bridge.Ready) return NotFound();
            parentOrigin ??= options.PortalOrigin;
            if (!bridge.AllowsParent(parentOrigin)) return BadRequest();
            var meeting = await conferences.FindById(id);
            if (meeting?.HrmMeeting == null) return NotFound();
            return Ok(new { enabled = true, parentOrigin, organizationId = meeting.HrmMeeting.Organization });
        }
        [HttpPost]
        public async Task<IActionResult> Create(Provision request)
        {
            if (!Service) return Unauthorized(new { message = "Meeting service authentication required." });
            if (request.StartsAt < DateTimeOffset.UtcNow.AddMinutes(-5) || request.EndsAt <= request.StartsAt ||
                request.EndsAt > request.StartsAt.AddHours(7) || request.StartsAt > DateTimeOffset.UtcNow.AddYears(1) ||
                request.Participants.Any(p => string.IsNullOrWhiteSpace(p) || p.Length > 100) ||
                !request.Participants.Contains(request.Organizer)) return BadRequest();
            var id = bridge.ConferenceId(request.Organization, request.ExternalId);
            var existing = await conferences.FindById(id);
            if (existing != null && (existing.HrmMeeting?.Organization != request.Organization || existing.HrmMeeting.ExternalId != request.ExternalId)) return Conflict();
            if (existing != null && (existing.Configuration.Name != request.Title || existing.Configuration.StartTime != request.StartsAt ||
                existing.HrmMeeting!.EndsAt != request.EndsAt || !existing.Configuration.Moderators.Contains(HrmMeetingBridge.ParticipantId(request.Organization, request.Organizer)) ||
                !existing.HrmMeeting.Participants.OrderBy(p => p).SequenceEqual(request.Participants.Distinct().Select(p => HrmMeetingBridge.ParticipantId(request.Organization, p)).OrderBy(p => p))))
                return Conflict(new { message = "The meeting request already exists with different details." });
            if (existing == null)
            {
                var conference = new Conference(id) {
                    HrmMeeting = new HrmMeetingOwnership { Organization = request.Organization, ExternalId = request.ExternalId, EndsAt = request.EndsAt,
                        Participants = request.Participants.Distinct().Select(p => HrmMeetingBridge.ParticipantId(request.Organization, p)).ToArray() },
                    Configuration = new ConferenceConfiguration { Name = request.Title, StartTime = request.StartsAt,
                        Moderators = new[] { HrmMeetingBridge.ParticipantId(request.Organization, request.Organizer) } },
                    Permissions = permissions.Default.ToDictionary(x => x.Key, x => x.Value.ToDictionary(v => v.Key, v => v.Value))
                };
                // Recording remains outside this integration's scope, including for the meeting organizer.
                foreach (var group in conference.Permissions.Values)
                {
                    group.Remove(DefinedPermissions.Recording.CanManage.Key);
                    group.Remove(DefinedPermissions.Permissions.CanGiveTemporaryPermission.Key);
                    group.Remove(DefinedPermissions.Chat.CanSendAnonymously.Key);
                }
                try { await conferences.Create(conference); }
                catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
                { return Conflict(new { message = "This meeting is being created. Retry the same request." }); }
            }
            return Ok(new { conferenceId = id, url = options.MeetingOrigin + "/hrm/" + id });
        }
        [HttpPost("{id}/tickets")]
        public async Task<IActionResult> Ticket(string id, Join request)
        {
            if (!Service) return Unauthorized(new { message = "Meeting service authentication required." });
            var meeting = await conferences.FindById(id);
            var now = DateTimeOffset.UtcNow;
            if (meeting?.HrmMeeting?.Organization != request.Organization) return NotFound();
            if (!meeting.HrmMeeting.Participants.Contains(HrmMeetingBridge.ParticipantId(request.Organization, request.Employee))) return NotFound();
            if (now < meeting.Configuration.StartTime?.AddMinutes(-15) || now >= meeting.HrmMeeting.EndsAt.AddMinutes(15)) return Conflict(new { message = "This meeting is outside its join window." });
            var until = request.AuthorizedUntil < meeting.HrmMeeting.EndsAt.AddMinutes(15) ? request.AuthorizedUntil : meeting.HrmMeeting.EndsAt.AddMinutes(15);
            if (until <= now || until > now.AddHours(8)) return BadRequest();
            var parent = request.ParentOrigin ?? options.PortalOrigin;
            if (!bridge.AllowsParent(parent)) return BadRequest();
            var ticket = bridge.Issue(request.Organization, id, request.Employee, request.Name, until, now, parent);
            return Ok(new { ticket, expiresIn = 60, frameUrl = options.MeetingOrigin + "/hrm/embed/" + id, parentOrigin = parent });
        }
        [HttpPost("{id}/redeem")]
        public IActionResult Redeem(string id, Redemption request)
        {
            if (!bridge.Ready) return NotFound();
            try {
                var result = bridge.Redeem(request.Ticket, id, DateTimeOffset.UtcNow);
                return Ok(new { accessToken = result.Token, subject = result.Subject, name = result.Name, expiresAt = result.Expires, parentOrigin = result.ParentOrigin });
            }
            catch (SecurityTokenException) { return Unauthorized(new { message = "Open this meeting from HRM again." }); }
        }
    }
}
