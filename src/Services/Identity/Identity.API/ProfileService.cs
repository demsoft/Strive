using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace Identity.API
{
    public class ProfileService : IProfileService
    {
        public Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            //>Processing
            var claims = new List<Claim>
            {
                context.Subject.Claims.First(x => x.Type == JwtClaimTypes.Name),
            };

            context.IssuedClaims.AddRange(claims);

            return Task.CompletedTask;
        }

        public Task IsActiveAsync(IsActiveContext context)
        {
            //>Processing
            context.IsActive = true;
            return Task.CompletedTask;
        }
    }
}