using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Strive.Hrm;
using Xunit;

namespace Strive.Tests.Auth
{
    public class HrmMeetingBridgeTests
    {
        private static HrmMeetingOptions Options() => new() { Enabled = true, ServiceSecret = new string('s', 64), SessionSecret = new string('t', 64), PortalOrigin = "https://hrm.example.test", MeetingOrigin = "https://meet.example.test" };
        [Fact] public void IntegrationIsClosedUnlessCompletelyConfigured()
        {
            Assert.False(new HrmMeetingBridge(new HrmMeetingOptions()).IsService(""));
            var options = Options(); options.Enabled = false;
            Assert.False(new HrmMeetingBridge(options).IsService(options.ServiceSecret));
            options.Enabled = true; options.PortalOrigin = "http://hrm.example.test";
            Assert.False(new HrmMeetingBridge(options).Ready);
        }
        [Fact] public void CandidateHandoffIsBoundToAnExplicitAllowedPortal()
        {
            var options = Options(); options.CandidatePortalOrigin = "https://careers.example.test";
            var bridge = new HrmMeetingBridge(options); var now = DateTimeOffset.UtcNow;
            var ticket = bridge.Issue("org", "room", "candidate:12", "Candidate", now.AddHours(1), now, options.CandidatePortalOrigin);
            Assert.Equal(options.CandidatePortalOrigin, bridge.Redeem(ticket, "room", now).ParentOrigin);
            Assert.False(bridge.AllowsParent("https://careers.example.test.evil.test"));
            Assert.Throws<ArgumentException>(() => bridge.Issue("org", "room", "candidate:12", "Candidate", now.AddHours(1), now, "https://evil.test"));
            Assert.NotEqual(HrmMeetingBridge.ParticipantId("org", "candidate:12"), HrmMeetingBridge.ParticipantId("org", "staff:12"));
        }
        [Fact] public void ServiceSecretMustMatchExactly()
        {
            var options = Options(); var bridge = new HrmMeetingBridge(options);
            Assert.True(bridge.IsService(options.ServiceSecret));
            Assert.False(bridge.IsService(options.ServiceSecret + "x")); Assert.False(bridge.IsService(null));
        }
        [Fact] public void ConferenceAndParticipantIdsAreStableAndOrganizationScoped()
        {
            var bridge = new HrmMeetingBridge(Options());
            Assert.Equal(bridge.ConferenceId("org", "request"), bridge.ConferenceId("org", "request"));
            Assert.NotEqual(bridge.ConferenceId("org", "request"), bridge.ConferenceId("other", "request"));
            Assert.NotEqual(HrmMeetingBridge.ParticipantId("org", "staff"), HrmMeetingBridge.ParticipantId("other", "staff"));
        }
        [Fact] public void SingleUseHandoffMintsOnlyAConferenceScopedSessionWithoutRoles()
        {
            var options = Options(); var bridge = new HrmMeetingBridge(options); var now = DateTimeOffset.UtcNow;
            var ticket = bridge.Issue("org", "room", "staff", "Test Staff", now.AddHours(1), now);
            var session = bridge.Redeem(ticket, "room", now);
            var user = new JwtSecurityTokenHandler().ValidateToken(session.Token, HrmMeetingBridge.Validation(options.SessionSecret), out _);
            Assert.Equal("room", user.FindFirst(HrmMeetingBridge.RoomClaim)?.Value);
            Assert.Equal("org", user.FindFirst("hrm_meeting_org")?.Value);
            Assert.Empty(user.FindAll(ClaimTypes.Role));
            Assert.True(HrmMeetingScope.AllowsRoom(user, "room")); Assert.False(HrmMeetingScope.AllowsRoom(user, "other"));
            Assert.Throws<SecurityTokenValidationException>(() => bridge.Redeem(ticket, "room", now));
        }
        [Fact] public void OnlyOneConcurrentRedemptionSucceeds()
        {
            var bridge = new HrmMeetingBridge(Options()); var now = DateTimeOffset.UtcNow;
            var ticket = bridge.Issue("org", "room", "staff", "Test", now.AddHours(1), now);
            var successes = 0;
            Parallel.For(0, 20, _ => { try { bridge.Redeem(ticket, "room", now); Interlocked.Increment(ref successes); } catch (SecurityTokenException) { } });
            Assert.Equal(1, successes);
        }
        [Fact] public void HandoffsExpireAfterSixtySeconds()
        {
            var bridge = new HrmMeetingBridge(Options()); var now = DateTimeOffset.UtcNow;
            var ticket = bridge.Issue("org", "room", "staff", "Test", now.AddHours(1), now);
            Assert.Throws<SecurityTokenValidationException>(() => bridge.Redeem(ticket, "room", now.AddSeconds(60)));
        }
        [Fact] public void HandoffsCannotBeUsedInAnotherRoom()
        {
            var bridge = new HrmMeetingBridge(Options()); var now = DateTimeOffset.UtcNow;
            var ticket = bridge.Issue("org", "room", "staff", "Test", now.AddHours(1), now);
            Assert.Throws<SecurityTokenValidationException>(() => bridge.Redeem(ticket, "other", now));
        }
        [Fact] public void GeneralApiAudienceAndOtherSigningKeysAreRejected()
        {
            var bridge = new HrmMeetingBridge(Options()); var now = DateTimeOffset.UtcNow;
            var ticket = bridge.Issue("org", "room", "staff", "Test", now.AddHours(1), now);
            var session = bridge.Redeem(ticket, "room", now);
            var wrongAudience = HrmMeetingBridge.Validation(Options().SessionSecret); wrongAudience.ValidAudience = "hrm-api";
            Assert.ThrowsAny<SecurityTokenException>(() => new JwtSecurityTokenHandler().ValidateToken(session.Token, wrongAudience, out _));
            Assert.ThrowsAny<SecurityTokenException>(() => new JwtSecurityTokenHandler().ValidateToken(session.Token, HrmMeetingBridge.Validation(new string('x', 64)), out _));
        }
        [Theory]
        [InlineData("/v1/hrm/meetings", "POST", "")]
        [InlineData("/v1/conference/room", "PATCH", "")]
        [InlineData("/v1/conference", "POST", "")]
        [InlineData("/v1/admin", "GET", "")]
        [InlineData("/equipment-signalr", "GET", "?conferenceId=room")]
        [InlineData("/signalr", "GET", "?conferenceId=other")]
        public void MeetingSessionsCannotCallGeneralApisOrOtherRooms(string path, string method, string query)
        {
            var context = new DefaultHttpContext(); context.Request.Path = path; context.Request.Method = method; context.Request.QueryString = new QueryString(query);
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("iss", HrmMeetingBridge.Issuer), new Claim(HrmMeetingBridge.RoomClaim, "room") }, "test"));
            Assert.False(HrmMeetingScope.AllowsRequest(context.Request, user));
        }
        [Fact] public void MeetingSessionsCanNegotiateOnlyTheirOwnHub()
        {
            var context = new DefaultHttpContext(); context.Request.Path = "/signalr/negotiate"; context.Request.QueryString = new QueryString("?conferenceId=room");
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("iss", HrmMeetingBridge.Issuer), new Claim(HrmMeetingBridge.RoomClaim, "room") }, "test"));
            Assert.True(HrmMeetingScope.AllowsRequest(context.Request, user));
        }
    }
}
