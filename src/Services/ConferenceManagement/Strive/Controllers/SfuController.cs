using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Strive.Config;
using Strive.Messaging.SFU;
using Strive.Messaging.SFU.Dto;

namespace Strive.Controllers
{
    [Route("v1/[controller]")]
    [ApiController]
    public class SfuController : Controller
    {
        private readonly ISfuConferenceInfoProvider _provider;
        private readonly SfuOptions _options;

        public SfuController(ISfuConferenceInfoProvider provider, IOptions<SfuOptions> options)
        {
            _provider = provider;
            _options = options.Value;
        }

        [HttpGet("{conferenceId}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<SfuConferenceInfo>> Fetch(string conferenceId, [FromQuery] string? apiKey)
        {
            var provided = Request.Headers["X-GOS-SFU-Key"].ToString();
            if (string.IsNullOrEmpty(provided)) provided = apiKey;
            if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(_options.ApiKey) ||
                !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(provided)), SHA256.HashData(Encoding.UTF8.GetBytes(_options.ApiKey))))
                return StatusCode(403, new { message = "Media service authentication required." });

            return await _provider.Get(conferenceId);
        }
    }
}
