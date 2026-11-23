using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Strive.Controllers;
using Strive.Core.Domain.Entities;
using Strive.Core.Services.ConferenceManagement.Gateways;
using Strive.Core.Services.Permissions;
using Strive.Core.Services.Permissions.Options;
using Strive.Hrm;
using Xunit;

namespace Strive.Tests.Controllers
{
    public class HrmMeetingsControllerTests
    {
        private Conference? saved;
        private readonly Mock<IConferenceRepo> repo = new();
        private readonly HrmMeetingsController controller;
        private readonly HrmMeetingOptions settings = new() { Enabled = true, ServiceSecret = new string('a', 64), SessionSecret = new string('b', 64), PortalOrigin = "https://hrm.example.test", MeetingOrigin = "https://meet.example.test" };
        public HrmMeetingsControllerTests()
        {
            repo.Setup(r => r.FindById(It.IsAny<string>())).ReturnsAsync(() => saved);
            repo.Setup(r => r.Create(It.IsAny<Conference>())).Callback<Conference>(c => saved = c).Returns(Task.CompletedTask);
            controller = new HrmMeetingsController(new HrmMeetingBridge(settings), settings, repo.Object, Options.Create(new DefaultPermissionOptions()));
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            controller.Request.Headers["X-GOS-HRM-Service"] = settings.ServiceSecret;
        }
        private HrmMeetingsController.Provision Draft() => new() { Organization = "org-a", ExternalId = "external-a", Organizer = "staff-a", Title = "Team check-in", StartsAt = DateTimeOffset.UtcNow.AddMinutes(1), EndsAt = DateTimeOffset.UtcNow.AddHours(1), Participants = new[] { "staff-a", "staff-b" } };
        [Fact] public async Task ProvisioningRequiresServiceAuthentication()
        { controller.Request.Headers.Remove("X-GOS-HRM-Service"); Assert.IsType<UnauthorizedObjectResult>(await controller.Create(Draft())); Assert.Null(saved); }
        [Fact] public async Task CreateReturnsAStableRoomAndSealsAttendeeScope()
        {
            var draft = Draft(); Assert.IsType<OkObjectResult>(await controller.Create(draft));
            Assert.Equal("org-a", saved!.HrmMeeting!.Organization); Assert.Equal(2, saved.HrmMeeting.Participants.Length);
            Assert.Single(saved.Configuration.Moderators);
            foreach (var group in saved.Permissions.Values) { Assert.False(group.ContainsKey(DefinedPermissions.Recording.CanManage.Key)); Assert.False(group.ContainsKey(DefinedPermissions.Permissions.CanGiveTemporaryPermission.Key)); }
            Assert.IsType<OkObjectResult>(await controller.Create(draft));
            repo.Verify(r => r.Create(It.IsAny<Conference>()), Times.Once);
            draft.Title = "Changed"; Assert.IsType<ConflictObjectResult>(await controller.Create(draft));
        }
        [Theory] [InlineData("org-b", "staff-a")] [InlineData("org-a", "uninvited")]
        public async Task OtherOrganizationsAndUninvitedEmployeesCannotGetTickets(string org, string employee)
        {
            await controller.Create(Draft());
            Assert.IsType<NotFoundResult>(await controller.Ticket(saved!.ConferenceId, new HrmMeetingsController.Join { Organization = org, Employee = employee, Name = "Test", AuthorizedUntil = DateTimeOffset.UtcNow.AddHours(1) }));
        }
        [Fact] public async Task CandidateTicketKeepsItsPortalAndRejectsUnlistedOrigins()
        {
            settings.CandidatePortalOrigin="https://careers.example.test";
            var draft=Draft();draft.Participants=new[]{"staff-a","candidate:12"};await controller.Create(draft);
            var request=new HrmMeetingsController.Join{Organization="org-a",Employee="candidate:12",Name="Candidate",AuthorizedUntil=DateTimeOffset.UtcNow.AddMinutes(30),ParentOrigin=settings.CandidatePortalOrigin};
            var result=Assert.IsType<OkObjectResult>(await controller.Ticket(saved!.ConferenceId,request));
            var json=System.Text.Json.JsonSerializer.Serialize(result.Value);Assert.Contains(settings.CandidatePortalOrigin,json);
            request.ParentOrigin="https://evil.test";Assert.IsType<BadRequestResult>(await controller.Ticket(saved.ConferenceId,request));
            Assert.IsType<BadRequestResult>(await controller.BootstrapMeeting(saved.ConferenceId,"https://evil.test"));
        }
        [Fact] public async Task JoinWindowIsEnforced()
        {
            var draft = Draft(); draft.StartsAt = DateTimeOffset.UtcNow.AddDays(1); draft.EndsAt = draft.StartsAt.AddHours(1); await controller.Create(draft);
            Assert.IsType<ConflictObjectResult>(await controller.Ticket(saved!.ConferenceId, new HrmMeetingsController.Join { Organization = "org-a", Employee = "staff-b", Name = "Test", AuthorizedUntil = DateTimeOffset.UtcNow.AddHours(1) }));
        }
    }
}
