using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Strive.Core.Services.Media
{
    public interface ITurnCredentialFactory
    {
        /// <summary>
        ///     Create the ice server entry for a participant, or null if TURN is not configured
        /// </summary>
        IceServerInfo? Create(Participant participant);
    }

    /// <summary>
    ///     Time-limited credentials for coturn's "use-auth-secret" mode (the TURN REST API scheme,
    ///     draft-uberti-behave-turn-rest): username = "{expiry unix time}:{user}" and
    ///     credential = base64(HMAC-SHA1(secret, username)). coturn recomputes the credential, so no accounts are needed.
    /// </summary>
    public class TurnCredentialFactory : ITurnCredentialFactory
    {
        private readonly TimeProvider _timeProvider;
        private readonly TurnOptions _options;

        public TurnCredentialFactory(IOptions<TurnOptions> options, TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
            _options = options.Value;
        }

        public IceServerInfo? Create(Participant participant)
        {
            if (!_options.IsEnabled) return null;

            var expiresAt = _timeProvider.GetUtcNow().Add(_options.CredentialLifetime).ToUnixTimeSeconds();
            var username = $"{expiresAt}:{participant.Id}";

            using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(_options.Secret!));
            var credential = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(username)));

            return new IceServerInfo(_options.Urls, username, credential);
        }
    }
}
