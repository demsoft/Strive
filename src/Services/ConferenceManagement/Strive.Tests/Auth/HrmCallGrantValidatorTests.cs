using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Strive.Auth;
using Xunit;

namespace Strive.Tests.Auth
{
    public class HrmCallGrantValidatorTests : IDisposable
    {
        private readonly RSA _rsa = RSA.Create(2048);
        private readonly DateTimeOffset _now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        private const string Issuer = "https://hrm.example.test";

        private HrmCallGrantValidator Validator => new(Issuer, new RsaSecurityKey(_rsa.ExportParameters(false)));

        private string Token(string organization = "org-a", string conference = "call-a", string subject = "employee-a",
            string purpose = HrmCallGrantValidator.Purpose, string audience = HrmCallGrantValidator.Audience,
            string issuer = Issuer, int start = -5, int end = 90, Claim? extra = null, bool includeJti = true)
        {
            var claims = new List<Claim>
            {
                new("sub", subject), new("organization_id", organization),
                new("conference_id", conference), new("purpose", purpose),
            };
            if (includeJti) claims.Add(new("jti", "grant-a"));
            if (extra != null) claims.Add(extra);
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer, audience, claims,
                _now.AddSeconds(start).UtcDateTime, _now.AddSeconds(end).UtcDateTime,
                new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256)));
        }

        [Fact]
        public void AcceptsSignedRoomBoundGrant()
        {
            var grant = Validator.Validate(Token(), "org-a", "call-a", _now);
            Assert.Equal(new HrmCallGrant("employee-a", "org-a", "call-a", "grant-a"), grant);
        }

        [Theory]
        [InlineData("org-b", "call-a")]
        [InlineData("org-a", "call-b")]
        [InlineData("", "call-a")]
        [InlineData("org-a", "")]
        public void RejectsDifferentOrMissingScope(string organization, string conference) =>
            Assert.ThrowsAny<SecurityTokenException>(() => Validator.Validate(Token(), organization, conference, _now));

        [Fact]
        public void RejectsExpiredGrant() => Assert.ThrowsAny<SecurityTokenException>(() =>
            Validator.Validate(Token(start: -90, end: -1), "org-a", "call-a", _now));

        [Fact]
        public void RejectsFutureGrant() => Assert.ThrowsAny<SecurityTokenException>(() =>
            Validator.Validate(Token(start: 1), "org-a", "call-a", _now));

        [Fact]
        public void RejectsLongLivedGrant() => Assert.ThrowsAny<SecurityTokenException>(() =>
            Validator.Validate(Token(end: 300), "org-a", "call-a", _now));

        [Fact]
        public void RejectsWrongIssuer() => Assert.ThrowsAny<SecurityTokenException>(() =>
            Validator.Validate(Token(issuer: "https://other.example.test"), "org-a", "call-a", _now));

        [Fact]
        public void RejectsGeneralApiToken() => Assert.ThrowsAny<SecurityTokenException>(() =>
            Validator.Validate(Token(audience: "hrm-api"), "org-a", "call-a", _now));

        [Fact]
        public void RejectsOtherPurpose() => Assert.ThrowsAny<SecurityTokenException>(() =>
            Validator.Validate(Token(purpose: "recording.join"), "org-a", "call-a", _now));

        [Fact]
        public void RejectsBlankSubject() => Assert.ThrowsAny<SecurityTokenException>(() =>
            Validator.Validate(Token(subject: ""), "org-a", "call-a", _now));

        [Fact]
        public void RejectsMissingGrantId() => Assert.ThrowsAny<SecurityTokenException>(() =>
            Validator.Validate(Token(includeJti: false), "org-a", "call-a", _now));

        [Theory]
        [InlineData("role")]
        [InlineData("roles")]
        [InlineData(ClaimTypes.Role)]
        [InlineData("sub")]
        [InlineData("organization_id")]
        public void RejectsRolesAndAmbiguousIdentity(string claim) => Assert.ThrowsAny<SecurityTokenException>(() =>
            Validator.Validate(Token(extra: new Claim(claim, "unexpected")), "org-a", "call-a", _now));

        [Fact]
        public void RejectsOtherSigningKey()
        {
            using var other = RSA.Create(2048);
            var validator = new HrmCallGrantValidator(Issuer, new RsaSecurityKey(other.ExportParameters(false)));
            Assert.ThrowsAny<SecurityTokenException>(() => validator.Validate(Token(), "org-a", "call-a", _now));
        }

        [Fact]
        public void RejectsUnsignedToken()
        {
            var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(Issuer,
                HrmCallGrantValidator.Audience, expires: _now.AddMinutes(1).UtcDateTime));
            Assert.ThrowsAny<SecurityTokenException>(() => Validator.Validate(token, "org-a", "call-a", _now));
        }

        [Fact]
        public void RequiresHttpsIssuer() => Assert.Throws<ArgumentException>(() =>
            new HrmCallGrantValidator("http://hrm.example.test", new RsaSecurityKey(_rsa)));

        public void Dispose() => _rsa.Dispose();
    }
}
