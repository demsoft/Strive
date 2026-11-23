using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Strive.Hrm
{
    public static class HrmMeetingScope
    {
        public static bool IsMeetingUser(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true && user.FindFirst("iss")?.Value == HrmMeetingBridge.Issuer;
        public static bool AllowsRequest(HttpRequest request, ClaimsPrincipal user)
        {
            if (!IsMeetingUser(user)) return true;
            var room = user.FindFirst(HrmMeetingBridge.RoomClaim)?.Value;
            if (string.IsNullOrWhiteSpace(room)) return false;
            return (request.Path.StartsWithSegments("/signalr") && request.Query["conferenceId"] == room) ||
                   (HttpMethods.IsGet(request.Method) && request.Path == "/v1/time");
        }
        public static bool AllowsRoom(ClaimsPrincipal user, string room) => !IsMeetingUser(user) ||
            user.FindFirst(HrmMeetingBridge.RoomClaim)?.Value == room;
    }
}
