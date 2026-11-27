using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Strive.Hrm
{
    public sealed class HrmMeetingOptions
    {
        public bool Enabled { get; set; }
        public string ServiceSecret { get; set; } = "";
        public string SessionSecret { get; set; } = "";
        public string PortalOrigin { get; set; } = "";
        public string MeetingOrigin { get; set; } = "";
        public string CandidatePortalOrigin { get; set; } = "";
    }

    // Production handoffs use atomic MongoDB redemption; the in-memory constructor is for isolated tests.
    public sealed class HrmMeetingBridge
    {
        public const string Scheme = "HrmMeeting";
        public const string Issuer = "goshrm-scheduled-meeting";
        public const string Audience = "gos-meet-hrm";
        public const string RoomClaim = "hrm_meeting_room";
        private readonly HrmMeetingOptions options;
        private readonly ConcurrentDictionary<string, Ticket> tickets = new();
        private sealed record Ticket(string Organization, string Room, string Subject, string Name, DateTimeOffset ValidUntil, DateTimeOffset RedeemUntil, string ParentOrigin);
        private readonly IMongoCollection<BsonDocument>? sharedTickets;
        public HrmMeetingBridge(HrmMeetingOptions options, IMongoCollection<BsonDocument>? sharedTickets = null)
        {
            this.options = options;
            this.sharedTickets = sharedTickets;
            sharedTickets?.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("redeemUntil"), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }));
        }
        private static string TicketHash(string ticket) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ticket)));
        private void SaveTicket(string ticket, Ticket value)
        {
            if (sharedTickets == null) { tickets[ticket] = value; return; }
            sharedTickets.InsertOne(new BsonDocument {
                { "_id", TicketHash(ticket) }, { "organization", value.Organization }, { "room", value.Room },
                { "subject", value.Subject }, { "name", value.Name }, { "parentOrigin", value.ParentOrigin },
                { "validUntil", value.ValidUntil.UtcDateTime }, { "redeemUntil", value.RedeemUntil.UtcDateTime }
            });
        }
        private bool TakeTicket(string ticket, string room, out Ticket? value)
        {
            if (sharedTickets == null) return tickets.TryRemove(ticket, out value);
            var document = sharedTickets.FindOneAndDelete(new BsonDocument { { "_id", TicketHash(ticket) }, { "room", room } });
            value = document == null ? null : new Ticket(document["organization"].AsString, document["room"].AsString,
                document["subject"].AsString, document["name"].AsString, document["validUntil"].ToUniversalTime(),
                document["redeemUntil"].ToUniversalTime(), document["parentOrigin"].AsString);
            return value != null;
        }
        public bool Ready => options.Enabled && options.ServiceSecret.Length >= 48 && options.SessionSecret.Length >= 48 &&
            SecureOrigin(options.PortalOrigin) && SecureOrigin(options.MeetingOrigin) &&
            (string.IsNullOrEmpty(options.CandidatePortalOrigin) || SecureOrigin(options.CandidatePortalOrigin));
        public bool AllowsParent(string value) => Ready && (value == options.PortalOrigin ||
            (!string.IsNullOrEmpty(options.CandidatePortalOrigin) && value == options.CandidatePortalOrigin));
        private static bool SecureOrigin(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme == "https" && uri.GetLeftPart(UriPartial.Authority) == value && uri.UserInfo.Length == 0;
        public bool IsService(string? provided) => Ready && provided != null &&
            CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(provided)), SHA256.HashData(Encoding.UTF8.GetBytes(options.ServiceSecret)));
        public string ConferenceId(string organization, string externalId)
        {
            if (!Ready) throw new InvalidOperationException("GOS Meet integration is not configured.");
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.ServiceSecret));
            return "hrm-" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(organization + "\0" + externalId))).ToLowerInvariant()[..32];
        }
        public static string ParticipantId(string organization, string employee) => "hrm-" +
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(organization + "\0" + employee))).ToLowerInvariant();
        public string Issue(string organization, string room, string employee, string name, DateTimeOffset expires, DateTimeOffset now, string? parentOrigin = null)
        {
            parentOrigin ??= options.PortalOrigin;
            if (!AllowsParent(parentOrigin) || string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(room) ||
                string.IsNullOrWhiteSpace(employee) || employee.Length > 100 || name.Length > 150 ||
                expires <= now || expires > now.AddHours(8)) throw new ArgumentException("Invalid meeting handoff.");
            foreach (var old in tickets.Where(x => x.Value.RedeemUntil <= now)) tickets.TryRemove(old.Key, out _);
            if (tickets.Count >= 5000 || (sharedTickets != null && sharedTickets.CountDocuments(
                new BsonDocument("redeemUntil", new BsonDocument("$gt", now.UtcDateTime)), new CountOptions { Limit = 5000 }) >= 5000)) throw new InvalidOperationException("Meeting handoffs are busy. Try again.");
            var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            SaveTicket(ticket, new Ticket(organization, room, ParticipantId(organization, employee), name, expires, now.AddSeconds(60), parentOrigin));
            return ticket;
        }
        public (string Token, string Subject, string Name, DateTimeOffset Expires, string ParentOrigin) Redeem(string ticket, string room, DateTimeOffset now)
        {
            if (!Ready || ticket.Length != 64 || !TakeTicket(ticket, room, out var value) || value == null || value.Room != room ||
                value.RedeemUntil <= now || value.ValidUntil <= now) throw new SecurityTokenValidationException("Meeting handoff expired or invalid.");
            var jwt = new JwtSecurityToken(Issuer, Audience, new[] {
                new Claim("sub", value.Subject), new Claim("name", value.Name),
                new Claim(RoomClaim, value.Room), new Claim("hrm_meeting_org", value.Organization),
                new Claim("jti", Guid.NewGuid().ToString("N"))
            }, now.UtcDateTime, value.ValidUntil.UtcDateTime,
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SessionSecret)), SecurityAlgorithms.HmacSha256));
            return (new JwtSecurityTokenHandler().WriteToken(jwt), value.Subject, value.Name, value.ValidUntil, value.ParentOrigin);
        }
        public static TokenValidationParameters Validation(string? secret) => new() {
            ValidIssuer = Issuer, ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(string.IsNullOrEmpty(secret) || secret.Length < 48
                ? RandomNumberGenerator.GetBytes(64) : Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            RequireSignedTokens = true, RequireExpirationTime = true, ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
        };
    }
}
