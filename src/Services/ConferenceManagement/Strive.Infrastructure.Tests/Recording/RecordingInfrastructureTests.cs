using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Web;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Strive.Infrastructure.Recording;
using Xunit;

namespace Strive.Infrastructure.Tests.Recording
{
    public class RecordingInfrastructureTests
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        private const string Secret = "0123456789abcdef0123456789abcdef";

        private class FixedTimeProvider : TimeProvider
        {
            public override DateTimeOffset GetUtcNow()
            {
                return Now;
            }
        }

        private static JwtRecorderJoinTokenFactory CreateFactory(string? secret = Secret)
        {
            return new JwtRecorderJoinTokenFactory(Options.Create(new RecorderOptions {TokenSecret = secret}),
                new FixedTimeProvider());
        }

        [Fact]
        public void JoinToken_ContainsRecorderIdentityAndIsSigned()
        {
            var token = CreateFactory().Create("rec1", "conf1", TimeSpan.FromHours(1));

            var principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidIssuer = RecorderOptions.TokenIssuer,
                ValidAudience = RecorderOptions.TokenAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                ValidateLifetime = false,
            }, out var validated);

            var jwt = (JwtSecurityToken) validated;
            Assert.Equal("recorder-rec1", jwt.Subject);
            Assert.Equal("conf1", jwt.Claims.Single(x => x.Type == JwtRecorderJoinTokenFactory.ConferenceIdClaim).Value);
            Assert.Equal("rec1", jwt.Claims.Single(x => x.Type == JwtRecorderJoinTokenFactory.RecordingIdClaim).Value);
            Assert.Equal(Now.UtcDateTime.AddHours(1), jwt.ValidTo, TimeSpan.FromSeconds(1));
            Assert.NotNull(principal);
        }

        [Fact]
        public void JoinToken_WrongSecret_IsRejected()
        {
            var token = CreateFactory().Create("rec1", "conf1", TimeSpan.FromHours(1));

            Assert.ThrowsAny<SecurityTokenException>(() => new JwtSecurityTokenHandler().ValidateToken(token,
                new TokenValidationParameters
                {
                    ValidIssuer = RecorderOptions.TokenIssuer,
                    ValidAudience = RecorderOptions.TokenAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret.Replace('a', 'b'))),
                    ValidateLifetime = false,
                }, out _));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("short")]
        public void JoinToken_SecretMissingOrTooShort_Throw(string? secret)
        {
            Assert.Throws<InvalidOperationException>(() =>
                CreateFactory(secret).Create("rec1", "conf1", TimeSpan.FromHours(1)));
        }

        [Fact]
        public void ParticipantId_CannotBeConfusedWithUserIds()
        {
            Assert.Equal("recorder-rec1", JwtRecorderJoinTokenFactory.ParticipantId("rec1"));
        }

        [Fact]
        public void PlaybackUrl_IsSignedExpiringAndPointsToTheBucket()
        {
            using var storage = new S3RecordingStorage(Options.Create(new RecordingStorageOptions
            {
                ServiceUrl = "https://account.r2.cloudflarestorage.com",
                Bucket = "strive-recordings",
                AccessKeyId = "key",
                SecretAccessKey = "secret",
            }));

            var url = storage.GetPlaybackUrl("recordings/conf1/rec1.mp4", TimeSpan.FromMinutes(30));

            Assert.Equal("account.r2.cloudflarestorage.com", url.Host.Replace("strive-recordings.", ""));
            Assert.Contains("recordings/conf1/rec1.mp4", url.AbsolutePath);
            var query = HttpUtility.ParseQueryString(url.Query);
            Assert.Equal("1800", query["X-Amz-Expires"]);
            Assert.False(string.IsNullOrEmpty(query["X-Amz-Signature"]));
            Assert.Equal("video/mp4", query["response-content-type"]);
        }

        [Fact]
        public void Storage_NotConfigured_DoesNotFailUntilUsed()
        {
            using var storage = new S3RecordingStorage(Options.Create(new RecordingStorageOptions()));

            Assert.NotNull(storage);
        }
    }
}
