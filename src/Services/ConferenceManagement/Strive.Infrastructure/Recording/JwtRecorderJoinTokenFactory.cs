using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Strive.Core.Services.Recording;
using Strive.Core.Services.Recording.Gateways;

namespace Strive.Infrastructure.Recording
{
    public class JwtRecorderJoinTokenFactory : IRecorderJoinTokenFactory
    {
        public const string RecordingIdClaim = "recording_id";
        public const string ConferenceIdClaim = "conference_id";

        /// <summary>
        ///     The participant id of a recorder. It cannot collide with real ids (hex encoded user names or user ids).
        /// </summary>
        public static string ParticipantId(string recordingId)
        {
            return RecorderParticipants.ParticipantId(recordingId);
        }

        private readonly RecorderOptions _options;
        private readonly TimeProvider _timeProvider;

        public JwtRecorderJoinTokenFactory(IOptions<RecorderOptions> options, TimeProvider timeProvider)
        {
            _options = options.Value;
            _timeProvider = timeProvider;
        }

        public string Create(string recordingId, string conferenceId, TimeSpan validFor)
        {
            if (string.IsNullOrEmpty(_options.TokenSecret) || _options.TokenSecret.Length < 32)
                throw new InvalidOperationException("Recording:Recorder:TokenSecret must be at least 32 characters.");

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.TokenSecret)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(RecorderOptions.TokenIssuer, RecorderOptions.TokenAudience, new[]
            {
                new Claim("sub", ParticipantId(recordingId)),
                new Claim("name", RecorderParticipants.DisplayName),
                new Claim("role", RecorderOptions.RoleClaimValue),
                new Claim(RecordingIdClaim, recordingId),
                new Claim(ConferenceIdClaim, conferenceId),
            }, now, now.Add(validFor), credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
