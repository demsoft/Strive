using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Strive.IntegrationTests._Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    /// <summary>
    ///     In production the web app is on another host than the API and IIS does not add CORS headers: the API does.
    /// </summary>
    [Collection(IntegrationTestCollection.Definition)]
    public class CorsTests : IntegrationTestBase
    {
        public CorsTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper, mongoDb)
        {
        }

        private HttpClient ClientWithOrigins(string? origins)
        {
            return Factory.WithWebHostBuilder(b =>
            {
                if (origins != null) b.UseSetting("Cors:AllowedOrigins", origins);
            }).CreateClient();
        }

        private static Task<HttpResponseMessage> Preflight(HttpClient client, string origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/signalr/negotiate");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "authorization,x-signalr-user-agent");
            return client.SendAsync(request);
        }

        [Fact]
        public async Task ConfiguredOrigin_PreflightIsAllowedWithCredentials()
        {
            var client = ClientWithOrigins("https://meet.example.com, https://other.example.com");

            var response = await Preflight(client, "https://meet.example.com");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal("https://meet.example.com",
                Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
            Assert.Contains("x-signalr-user-agent",
                string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")).ToLowerInvariant());
        }

        [Fact]
        public async Task OtherOrigin_IsNotAllowed()
        {
            var client = ClientWithOrigins("https://meet.example.com");

            var response = await Preflight(client, "https://evil.example.com");

            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }

        [Fact]
        public async Task NothingConfigured_NoCorsHeaders()
        {
            var client = ClientWithOrigins(null);

            var response = await Preflight(client, "https://meet.example.com");

            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }
    }
}
