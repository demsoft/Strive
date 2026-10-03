#nullable enable
using System;
using MongoDB.Bson.Serialization.Attributes;

namespace Identity.API.Accounts
{
    public class StriveUser
    {
        /// <summary>The subject id. A random id, so it cannot collide with the hex ids of demo users.</summary>
        [BsonId]
        public string Id { get; set; } = null!;

        public string Email { get; set; } = null!;
        public string NormalizedEmail { get; set; } = null!;

        /// <summary>The name shown to other participants. Null until the person has chosen one.</summary>
        public string? DisplayName { get; set; }

        /// <summary>The name the sign in provider knows, offered as the default display name.</summary>
        public string? SuggestedName { get; set; }

        public string? PasswordHash { get; set; }
        public bool EmailConfirmed { get; set; }

        public int AccessFailedCount { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public bool HasDisplayName => !string.IsNullOrWhiteSpace(DisplayName);
    }

    /// <summary>A Google (or other external) account that is linked to a user. The id is "provider:key", unique.</summary>
    public class ExternalLogin
    {
        [BsonId]
        public string Id { get; set; } = null!;

        public string UserId { get; set; } = null!;

        public static string BuildId(string provider, string key) => $"{provider.ToLowerInvariant()}:{key}";
    }

    public enum TokenPurpose
    {
        ConfirmEmail,
        ResetPassword,
    }

    /// <summary>A single use token that was sent by email. Only its hash is stored.</summary>
    public class UserToken
    {
        [BsonId]
        public string Id { get; set; } = null!;

        public string UserId { get; set; } = null!;
        public TokenPurpose Purpose { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
