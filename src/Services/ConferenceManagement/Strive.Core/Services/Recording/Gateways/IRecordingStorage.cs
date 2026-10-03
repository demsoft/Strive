using System;
using System.Threading;
using System.Threading.Tasks;

namespace Strive.Core.Services.Recording.Gateways
{
    /// <summary>
    ///     The place where finished recordings are stored. The recorder uploads the file itself, the server only hands out
    ///     links and deletes files. Implementations: S3 compatible stores (Cloudflare R2, MinIO, AWS S3).
    /// </summary>
    public interface IRecordingStorage
    {
        /// <summary>
        ///     A temporary link that plays the file in a browser (with seeking)
        /// </summary>
        Uri GetPlaybackUrl(string storageKey, TimeSpan validFor);

        Task Delete(string storageKey, CancellationToken cancellationToken = default);

        Task<bool> Exists(string storageKey, CancellationToken cancellationToken = default);
    }
}
