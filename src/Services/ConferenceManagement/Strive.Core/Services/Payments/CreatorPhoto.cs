using System;

namespace Strive.Core.Services.Payments
{
    /// <summary>The picture of a creator, small enough to live in the database (the browser shrinks it before sending).</summary>
    public class CreatorPhoto
    {
        public CreatorPhoto(string creatorId, string contentType, byte[] data, DateTimeOffset updatedAt)
        {
            CreatorId = creatorId;
            ContentType = contentType;
            Data = data;
            UpdatedAt = updatedAt;
        }

        public string CreatorId { get; init; }

        public string ContentType { get; set; }

        public byte[] Data { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }

    public static class PhotoRules
    {
        /// <summary>The page shrinks a photo to some tens of kilobytes; this is what the server accepts at most</summary>
        public const int MaxBytes = 300 * 1024;

        /// <summary>
        ///     What the bytes really are, from their first bytes and not from what the sender says: jpeg, png or webp.
        ///     Everything else (an svg with a script in it, a page, a program) is no photo.
        /// </summary>
        public static string? Sniff(byte[] data)
        {
            if (data.Length < 12) return null;
            if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return "image/jpeg";
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 && data[4] == 0x0D &&
                data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
                return "image/png";
            // RIFF....WEBP
            if (data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 && data[8] == 0x57 &&
                data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
                return "image/webp";
            return null;
        }
    }
}
