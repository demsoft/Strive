using System;
using System.Security.Cryptography;

namespace Strive.Core.Services.Recording
{
    public static class RecordingTokens
    {
        /// <summary>
        ///     A random url safe string, used for the share link
        /// </summary>
        public static string CreateShareToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', '-').Replace('/', '_')
                .TrimEnd('=');
        }

        public static string CreateRecordingId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static string BuildStorageKey(string conferenceId, string recordingId)
        {
            return $"recordings/{conferenceId}/{recordingId}.mp4";
        }
    }
}
