using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Strive.Core.Services.Payments;
using Strive.Infrastructure.Serialization;
using Strive.IntegrationTests._Helpers;
using Strive.Models.Response;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    /// <summary>The photos of the creators when they are kept at an image service (Cloudinary) and not in the database.</summary>
    [Collection(IntegrationTestCollection.Definition)]
    public class CloudinaryPhotoTests : ServiceIntegrationTest
    {
        private class FakeStorage : IPhotoStorage
        {
            public ConcurrentQueue<(string CreatorId, string ContentType, byte[] Data)> Saved { get; } = new();
            public ConcurrentQueue<string?> Removed { get; } = new();
            public bool Failing { get; set; }
            private int _version;

            public Task<StoredPhoto> Save(string creatorId, string contentType, byte[] data,
                CancellationToken cancellationToken = default)
            {
                if (Failing) throw new PhotoStorageException("Cloudinary answered 500.");
                Saved.Enqueue((creatorId, contentType, data));
                return Task.FromResult(new StoredPhoto("v" + Interlocked.Increment(ref _version), "gbedu/creators/" + creatorId));
            }

            public Task Remove(string creatorId, string? publicId, CancellationToken cancellationToken = default)
            {
                Removed.Enqueue(publicId);
                return Task.CompletedTask;
            }

            public string? RedirectUrl(string publicId, string version, int size) =>
                $"https://cdn.test/{size}/{version}/{publicId}";
        }

        private readonly FakeStorage _storage = new();

        public CloudinaryPhotoTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(
            testOutputHelper, mongoDb)
        {
        }

        private HttpClient Api(UserAccount? user)
        {
            var host = Factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
                services.AddSingleton<IPhotoStorage>(_storage)));
            var client = host.CreateClient();
            client.DefaultRequestHeaders.Authorization = user == null
                ? null
                : new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, user.Token);
            return client;
        }

        private async Task<(UserAccount host, string creatorId)> Creator()
        {
            var host = Factory.CreateUser("host" + Guid.NewGuid().ToString("N")[..6], false);
            var api = Api(host);
            var response = await api.PutAsync("/v1/creators/me",
                new JsonNetContent(new SaveCreatorDto("Ada Lovelace", "Piano", "058", "0123456789")));
            response.EnsureSuccessStatusCode();
            var profile = JsonConvert.DeserializeObject<CreatorProfileDto>(await response.Content.ReadAsStringAsync(),
                JsonConfig.Default)!;
            return (host, profile.CreatorId);
        }

        private static byte[] Jpeg(int size = 2000) =>
            new byte[] {0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte) 'J', (byte) 'F', (byte) 'I', (byte) 'F', 0, 1}
                .Concat(Enumerable.Repeat((byte) 7, size)).ToArray();

        private async Task<HttpResponseMessage> Put(UserAccount host, byte[] bytes)
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return await Api(host).PutAsync("/v1/creators/me/photo", content);
        }

        private static async Task<CreatorProfileDto> Profile(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<CreatorProfileDto>(await response.Content.ReadAsStringAsync(),
                JsonConfig.Default)!;
        }

        // the client of the test must not follow redirects: they are what is tested
        private async Task<HttpResponseMessage> GetPhoto(string idOrName, string query = "")
        {
            var host = Factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
                services.AddSingleton<IPhotoStorage>(_storage)));
            var client = host.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
            return await client.GetAsync($"/v1/creators/{idOrName}/photo{query}");
        }

        [Fact]
        public async Task Upload_GoesToTheImageService_NotIntoTheDatabase()
        {
            var (host, creatorId) = await Creator();
            var bytes = Jpeg();

            var profile = await Profile(await Put(host, bytes));

            var saved = Assert.Single(_storage.Saved);
            Assert.Equal(creatorId, saved.CreatorId);
            Assert.Equal("image/jpeg", saved.ContentType);
            Assert.Equal(bytes, saved.Data);
            Assert.Equal("v1", profile.PhotoVersion);
        }

        [Fact]
        public async Task Photo_IsSentToTheCdnAtTheSizeThatWasAsked()
        {
            var (host, creatorId) = await Creator();
            var profile = await Profile(await Put(host, Jpeg()));

            var small = await GetPhoto(creatorId, $"?v={profile.PhotoVersion}&w=64");
            var medium = await GetPhoto(creatorId, "?w=100");
            var huge = await GetPhoto(creatorId, "?w=99999");
            var unspecified = await GetPhoto(creatorId);

            Assert.Equal(HttpStatusCode.Redirect, small.StatusCode);
            Assert.Equal($"https://cdn.test/64/v1/gbedu/creators/{creatorId}", small.Headers.Location!.ToString());
            Assert.Equal($"https://cdn.test/128/v1/gbedu/creators/{creatorId}", medium.Headers.Location!.ToString());
            Assert.Equal($"https://cdn.test/400/v1/gbedu/creators/{creatorId}", huge.Headers.Location!.ToString());
            Assert.Equal($"https://cdn.test/400/v1/gbedu/creators/{creatorId}", unspecified.Headers.Location!.ToString());
        }

        [Fact]
        public async Task Photo_TheAddressOfTheCurrentVersionMayBeCachedForADay_AnyOtherOnlyBriefly()
        {
            var (host, creatorId) = await Creator();
            var profile = await Profile(await Put(host, Jpeg()));

            var current = await GetPhoto(creatorId, $"?v={profile.PhotoVersion}&w=64");
            var stale = await GetPhoto(creatorId, "?v=old&w=64");
            var without = await GetPhoto(creatorId, "?w=64");

            Assert.Contains("max-age=86400", current.Headers.CacheControl!.ToString());
            Assert.Contains("max-age=60", stale.Headers.CacheControl!.ToString());
            Assert.Contains("max-age=60", without.Headers.CacheControl!.ToString());
        }

        [Fact]
        public async Task Replaced_GetsANewVersion_AndItsAddressChanges()
        {
            var (host, creatorId) = await Creator();
            var first = await Profile(await Put(host, Jpeg()));

            var second = await Profile(await Put(host, Jpeg(3000)));

            Assert.NotEqual(first.PhotoVersion, second.PhotoVersion);
            Assert.Equal($"https://cdn.test/400/{second.PhotoVersion}/gbedu/creators/{creatorId}",
                (await GetPhoto(creatorId)).Headers.Location!.ToString());
        }

        [Fact]
        public async Task Removed_IsDeletedAtTheImageService_AndGone()
        {
            var (host, creatorId) = await Creator();
            await Put(host, Jpeg());

            var profile = await Profile(await Api(host).DeleteAsync("/v1/creators/me/photo"));

            Assert.Null(profile.PhotoVersion);
            Assert.Equal($"gbedu/creators/{creatorId}", Assert.Single(_storage.Removed));
            Assert.Equal(HttpStatusCode.NotFound, (await GetPhoto(creatorId)).StatusCode);
        }

        [Fact]
        public async Task ImageServiceDown_UploadIsRefusedWithAMessage_AndTheOldPhotoStays()
        {
            var (host, creatorId) = await Creator();
            var first = await Profile(await Put(host, Jpeg()));
            _storage.Failing = true;

            var response = await Put(host, Jpeg(3000));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("Payments_InvalidPhoto", body);
            Assert.DoesNotContain("Cloudinary", body); // what is wrong at the service is for the logs, not for the person
            var current = await GetPhoto(creatorId);
            Assert.Equal($"https://cdn.test/400/{first.PhotoVersion}/gbedu/creators/{creatorId}",
                current.Headers.Location!.ToString());
        }

        [Fact]
        public async Task WhatIsNoPictureOrTooBig_NeverReachesTheImageService()
        {
            var (host, _) = await Creator();

            var html = await Put(host, Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>"));
            var big = await Put(host, Jpeg(PhotoRules.MaxBytes + 10));
            var empty = await Put(host, Array.Empty<byte>());

            Assert.Equal(HttpStatusCode.BadRequest, html.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, big.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
            Assert.Empty(_storage.Saved);
        }
    }
}
