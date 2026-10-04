#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Services;
using Identity.API.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.API.Quickstart.Account
{
    /// <summary>Sign in with an external provider (Google) and the choice of the display name that follows.</summary>
    [SecurityHeaders]
    [AllowAnonymous]
    [AccountsModeOnly]
    public class ExternalController : SignInControllerBase
    {
        /// <summary>The user a half finished external sign in belongs to (waiting for the display name).</summary>
        private const string PendingUserClaim = "strive_pending_user";

        private const string ReturnUrlKey = "returnUrl";

        private readonly AccountService _accounts;
        private readonly ILogger<ExternalController> _logger;

        public ExternalController(IIdentityServerInteractionService interaction, IEventService events,
            IOptions<AccountsOptions> options, AccountService accounts, ILogger<ExternalController> logger)
            : base(interaction, events, options)
        {
            _accounts = accounts;
            _logger = logger;
        }

        /// <summary>Send the visitor to Google.</summary>
        [HttpGet]
        [EnableRateLimiting("account")]
        public IActionResult Challenge(string? returnUrl)
        {
            if (!Options.Google.IsConfigured) return NotFound();
            if (!string.IsNullOrEmpty(returnUrl) && !IsValidReturnUrl(returnUrl)) return BadRequest("invalid return URL");

            var props = new AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(Callback)),
                Items = {{ReturnUrlKey, returnUrl ?? string.Empty}},
            };
            return Challenge(props, GoogleProvider);
        }

        public const string GoogleProvider = "Google";

        /// <summary>Google sends the visitor back here.</summary>
        [HttpGet]
        public async Task<IActionResult> Callback()
        {
            var result = await HttpContext.AuthenticateAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
            if (result.Succeeded != true || result.Principal == null)
                return await FailAsync(null, "The sign in with Google did not work. Please try again.");

            var returnUrl = result.Properties?.Items.TryGetValue(ReturnUrlKey, out var value) == true ? value : null;
            var principal = result.Principal;

            var key = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(JwtClaimTypes.Subject);
            var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue(JwtClaimTypes.Email);
            var verified = string.Equals(principal.FindFirstValue(JwtClaimTypes.EmailVerified), "true",
                System.StringComparison.OrdinalIgnoreCase);
            if (key == null) return await FailAsync(returnUrl, "The sign in with Google did not work. Please try again.");

            var outcome = await _accounts.ExternalSignInAsync(GoogleProvider, key, email, verified,
                principal.FindFirstValue(ClaimTypes.Name));
            switch (outcome.Status)
            {
                case ExternalSignInStatus.EmailNotVerified:
                    return await FailAsync(returnUrl, "Google has not verified the email address of this account.");
                case ExternalSignInStatus.DomainNotAllowed:
                    _logger.LogInformation("A Google sign up was rejected because of the email domain");
                    return await FailAsync(returnUrl,
                        "Accounts can only be created with an email address of an allowed domain.");
            }

            var user = outcome.User!;
            if (!user.HasDisplayName)
            {
                // keep the half finished sign in in the external cookie while the visitor picks a name
                var pending = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] {new Claim(PendingUserClaim, user.Id)}, "pending"));
                var props = new AuthenticationProperties {Items = {{ReturnUrlKey, returnUrl ?? string.Empty}}};
                await HttpContext.SignInAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme, pending, props);
                return RedirectToAction(nameof(Welcome));
            }

            await HttpContext.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
            return await CompleteSignInAsync(user.Id, user.DisplayName!, user.Email, returnUrl);
        }

        /// <summary>The first sign in: choose the name other participants see.</summary>
        [HttpGet]
        public async Task<IActionResult> Welcome()
        {
            var pending = await GetPendingAsync();
            if (pending == null) return RedirectToAction("Login", "Account");

            return View(new WelcomeModel {DisplayName = pending.Value.User.SuggestedName});
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("account")]
        public async Task<IActionResult> Welcome(WelcomeModel model)
        {
            var pending = await GetPendingAsync();
            if (pending == null) return RedirectToAction("Login", "Account");

            var error = await _accounts.SetDisplayNameAsync(pending.Value.User.Id, model.DisplayName);
            if (error != null)
            {
                ModelState.AddModelError(nameof(model.DisplayName), error);
                return View(model);
            }

            await HttpContext.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
            return await CompleteSignInAsync(pending.Value.User.Id, model.DisplayName!.Trim(), pending.Value.User.Email,
                pending.Value.ReturnUrl);
        }

        private async Task<(StriveUser User, string? ReturnUrl)?> GetPendingAsync()
        {
            var result = await HttpContext.AuthenticateAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
            var id = result.Principal?.FindFirstValue(PendingUserClaim);
            if (result.Succeeded != true || id == null) return null;

            var user = await _accounts.FindByIdAsync(id);
            if (user == null) return null;

            var returnUrl = result.Properties?.Items.TryGetValue(ReturnUrlKey, out var value) == true ? value : null;
            return (user, returnUrl);
        }

        private async Task<IActionResult> FailAsync(string? returnUrl, string message)
        {
            await HttpContext.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
            TempData["Error"] = message;
            return RedirectToAction("Login", "Account", new {returnUrl});
        }
    }

    public class WelcomeModel
    {
        [Display(Name = "Your name in meetings")]
        public string? DisplayName { get; set; }
    }
}
