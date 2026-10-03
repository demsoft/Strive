using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Strive.Core.Services.Recording.Gateways;

namespace Strive.Infrastructure.Recording
{
    public class S3RecordingStorage : IRecordingStorage, IDisposable
    {
        private readonly Lazy<AmazonS3Client> _lazyClient;
        private readonly string _bucket;

        private AmazonS3Client _client => _lazyClient.Value;

        public S3RecordingStorage(IOptions<RecordingStorageOptions> options)
        {
            var value = options.Value;
            _bucket = value.Bucket;

            // created on first use, the storage is not configured if recording is switched off
            _lazyClient = new Lazy<AmazonS3Client>(() =>
            {
                var config = new AmazonS3Config
                {
                    ServiceURL = value.ServiceUrl,
                    AuthenticationRegion = value.Region,
                    ForcePathStyle = value.ForcePathStyle,
                };

                return new AmazonS3Client(value.AccessKeyId, value.SecretAccessKey, config);
            });
        }

        public Uri GetPlaybackUrl(string storageKey, TimeSpan validFor)
        {
            var url = _client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _bucket,
                Key = storageKey,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(validFor),
                ResponseHeaderOverrides = new ResponseHeaderOverrides
                {
                    ContentType = "video/mp4", ContentDisposition = "inline",
                },
            });

            return new Uri(url);
        }

        public async Task Delete(string storageKey, CancellationToken cancellationToken = default)
        {
            // deleting a key that does not exist is not an error in S3
            await _client.DeleteObjectAsync(_bucket, storageKey, cancellationToken);
        }

        public async Task<bool> Exists(string storageKey, CancellationToken cancellationToken = default)
        {
            try
            {
                await _client.GetObjectMetadataAsync(_bucket, storageKey, cancellationToken);
                return true;
            }
            catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_lazyClient.IsValueCreated) _client.Dispose();
        }
    }
}
