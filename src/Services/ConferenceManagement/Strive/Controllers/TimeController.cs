using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Strive.Controllers
{
    /// <summary>
    ///     The time of the server. Clients that play something together (watch together) use it to correct the
    ///     difference between their clock and the clock that the playback positions are based on.
    /// </summary>
    [ApiController]
    [Route("v1/time")]
    [AllowAnonymous]
    public class TimeController : ControllerBase
    {
        private readonly TimeProvider _timeProvider;

        public TimeController(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        [HttpGet]
        public ActionResult<TimeResponse> Get()
        {
            Response.Headers.CacheControl = "no-store";
            return new TimeResponse(_timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        }
    }

    public record TimeResponse(long UtcMilliseconds);
}
