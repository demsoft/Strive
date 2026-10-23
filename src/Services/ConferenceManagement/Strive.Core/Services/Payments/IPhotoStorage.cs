using System;
using System.Threading;
using System.Threading.Tasks;

namespace Strive.Core.Services.Payments
{
    /// <param name="Version">Changes with the picture: it is part of the address, so caches renew</param>
    /// <param name="PublicId">The name of the picture at the storage service; null if it is kept in the database</param>
    public record StoredPhoto(string Version, string? PublicId);

    /// <summary>Where the photos of the creators are kept: in the database, or at an image service with a CDN (Cloudinary).</summary>
    public interface IPhotoStorage
    {
        /// <summary>Keeps the picture (it was checked before). Throws <see cref="PhotoStorageException" /> if that did not work.</summary>
        Task<StoredPhoto> Save(string creatorId, string contentType, byte[] data,
            CancellationToken cancellationToken = default);

        /// <summary>Deletes the picture. A failure is logged and not thrown: the profile must not keep a photo that is not wanted.</summary>
        Task Remove(string creatorId, string? publicId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Where the browser is sent to get the picture at this size (a square, in pixels): the CDN of the image
        ///     service. Null if the pictures are kept in the database and the API sends them itself.
        /// </summary>
        string? RedirectUrl(string publicId, string version, int size);
    }

    public class PhotoStorageException : Exception
    {
        public PhotoStorageException(string message, Exception? inner = null) : base(message, inner)
        {
        }
    }

    /// <summary>Settings of the Cloudinary account (configuration section "Cloudinary"). The secret is never in a committed file.</summary>
    public class CloudinaryOptions
    {
        public const string Section = "Cloudinary";

        public string? CloudName { get; set; }

        public string? ApiKey { get; set; }

        public string? ApiSecret { get; set; }

        /// <summary>The folder (at Cloudinary) the pictures of the creators are kept in</summary>
        public string Folder { get; set; } = "gbedu/creators";

        public string ApiBaseUrl { get; set; } = "https://api.cloudinary.com";

        public string DeliveryBaseUrl { get; set; } = "https://res.cloudinary.com";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(CloudName) && !string.IsNullOrWhiteSpace(ApiKey) &&
                                    !string.IsNullOrWhiteSpace(ApiSecret);
    }

    public static class PhotoSizes
    {
        /// <summary>The sizes pictures are asked in: few, so that few versions are made (and stored) at the image service</summary>
        public static readonly int[] Buckets = {64, 128, 256, 400};

        /// <summary>The smallest size that is at least what was asked for (the biggest if more was asked)</summary>
        public static int Bucket(int? asked)
        {
            foreach (var size in Buckets)
                if (asked is { } wanted && wanted <= size)
                    return size;
            return Buckets[^1];
        }
    }
}
