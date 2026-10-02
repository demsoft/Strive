using System;
using Microsoft.Extensions.Options;
using Strive.Core.Services;
using Strive.Core.Services.Media;
using Xunit;

namespace Strive.Core.Tests.Services.Media
{
    public class TurnCredentialFactoryTests
    {
        // 2023-11-14 22:13:20 UTC, 1700000000 in unix time
        private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1700000000);

        private class FixedTimeProvider : TimeProvider
        {
            public override DateTimeOffset GetUtcNow()
            {
                return Now;
            }
        }

        private static TurnCredentialFactory Create(TurnOptions options)
        {
            return new(Options.Create(options), new FixedTimeProvider());
        }

        private static TurnOptions EnabledOptions()
        {
            return new()
            {
                Urls = new[] {"turn:turn.example.com:3478?transport=udp", "turn:turn.example.com:3478?transport=tcp"},
                Secret = "test-secret",
                CredentialLifetime = TimeSpan.FromSeconds(86400),
            };
        }

        [Fact]
        public void Create_Enabled_UsernameContainsExpiryAndParticipant()
        {
            var result = Create(EnabledOptions()).Create(new Participant("conference", "participant1"));

            Assert.NotNull(result);
            Assert.Equal("1700086400:participant1", result!.Username);
        }

        [Fact]
        public void Create_Enabled_CredentialMatchesCoturnsHmacSha1()
        {
            // the expected value was computed independently: python3 hmac.new(secret, username, sha1), base64
            var result = Create(EnabledOptions()).Create(new Participant("conference", "participant1"));

            Assert.Equal("c6Z3D2eisy1LJmc9eFXz4cL+9qM=", result!.Credential);
        }

        [Fact]
        public void Create_Enabled_ReturnConfiguredUrls()
        {
            var options = EnabledOptions();

            var result = Create(options).Create(new Participant("conference", "participant1"));

            Assert.Equal(options.Urls, result!.Urls);
        }

        [Fact]
        public void Create_OtherSecretAndLifetime_ChangeCredential()
        {
            var options = new TurnOptions
            {
                Urls = new[] {"turn:turn.example.com"},
                Secret = "another secret!",
                CredentialLifetime = TimeSpan.FromHours(1),
            };

            var result = Create(options).Create(new Participant("conference", "a b"));

            Assert.Equal("1700003600:a b", result!.Username);
            Assert.Equal("64dpGMRdcSaXQM0+gGanxF/DHc4=", result.Credential);
        }

        [Fact]
        public void Create_NoUrls_ReturnNull()
        {
            var options = EnabledOptions();
            options.Urls = Array.Empty<string>();

            Assert.Null(Create(options).Create(new Participant("conference", "participant1")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Create_NoSecret_ReturnNull(string? secret)
        {
            var options = EnabledOptions();
            options.Secret = secret;

            Assert.Null(Create(options).Create(new Participant("conference", "participant1")));
        }
    }
}
