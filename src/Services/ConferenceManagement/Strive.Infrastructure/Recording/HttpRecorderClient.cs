using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Strive.Core.Services.Recording.Gateways;

namespace Strive.Infrastructure.Recording
{
    public class HttpRecorderClient : IRecorderClient
    {
        private readonly HttpClient _client;
        private readonly RecorderOptions _options;

        public HttpRecorderClient(HttpClient client, IOptions<RecorderOptions> options)
        {
            _client = client;
            _options = options.Value;
        }

        public async Task Start(RecorderStartCommand command, CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(HttpMethod.Post, "recordings");
            request.Content = JsonContent.Create(command);

            using var response = await _client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public async Task Stop(string recordingId, CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(HttpMethod.Post, $"recordings/{Uri.EscapeDataString(recordingId)}/stop");

            using var response = await _client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string path)
        {
            if (string.IsNullOrEmpty(_options.BaseUrl))
                throw new InvalidOperationException("Recording:Recorder:BaseUrl is not configured.");

            var baseUri = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
            var request = new HttpRequestMessage(method, new Uri(baseUri, path));
            request.Headers.Add(RecorderOptions.SecretHeader, _options.SharedSecret);
            return request;
        }
    }
}
