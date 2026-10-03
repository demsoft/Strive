#nullable enable
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Services;
using Identity.API.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Identity.API.Quickstart.Account
{
    /// <summary>Create an account with an email address and a password, confirm the address, reset the password.</summary>
    [SecurityHeaders]
    [AllowAnonymous]
    [AccountsModeOnly]
    public class RegistrationController : SignInControllerBase
    {
        private readonly AccountService _accounts;

        public RegistrationController(IIdentityServerInteractionService interaction, IEventService events,
            IOptions<AccountsOptions> options, AccountService accounts)
            : base(interaction, events, options)
        {
            _accounts = accounts;
        }

        // ---- register

        [HttpGet]
        public IActionResult Register(string? returnUrl) => View(new RegisterModel {ReturnUrl = returnUrl});

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("account")]
        public async Task<IActionResult> Register(RegisterModel model)
        {
            var returnUrl = SafeReturnUrl(model.ReturnUrl);
            var outcome = await _accounts.RegisterAsync(model.Email, model.DisplayName, model.Password,
                token => AbsoluteUrl(nameof(ConfirmEmail), "Registration", new {token, returnUrl}),
                () => AbsoluteUrl("Login", "Account", new {returnUrl}),
                () => AbsoluteUrl(nameof(ForgotPassword), "Registration", new {returnUrl}));

            if (!outcome.Accepted)
            {
                foreach (var error in outcome.Errors) ModelState.AddModelError(string.Empty, error);
                model.Password = null;
                return View(model);
            }

            return RedirectToAction(nameof(CheckEmail), new {email = model.Email?.Trim(), returnUrl});
        }

        [HttpGet]
        public IActionResult CheckEmail(string? email, string? returnUrl) =>
            View(new CheckEmailModel {Email = email, ReturnUrl = returnUrl});

        // ---- confirm the email address

        /// <summary>
        ///     Only shows a button: mail scanners open links, and opening must not use up the token.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string? token, string? returnUrl)
        {
            if (!await _accounts.IsTokenValidAsync(token, TokenPurpose.ConfirmEmail))
                return View("LinkExpired", new LinkExpiredModel {Resend = true, ReturnUrl = returnUrl});

            return View(new ConfirmEmailModel {Token = token, ReturnUrl = returnUrl});
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("account")]
        public async Task<IActionResult> ConfirmEmail(ConfirmEmailModel model)
        {
            var user = await _accounts.ConfirmEmailAsync(model.Token);
            if (user == null) return View("LinkExpired", new LinkExpiredModel {Resend = true, ReturnUrl = model.ReturnUrl});

            TempData["Notice"] = "Your email address is confirmed. You can sign in now.";
            return RedirectToAction("Login", "Account", new {returnUrl = SafeReturnUrl(model.ReturnUrl)});
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("account")]
        public async Task<IActionResult> ResendConfirmation(string? email, string? returnUrl)
        {
            returnUrl = SafeReturnUrl(returnUrl);
            await _accounts.ResendConfirmationAsync(email,
                token => AbsoluteUrl(nameof(ConfirmEmail), "Registration", new {token, returnUrl}));
            return RedirectToAction(nameof(CheckEmail), new {email = email?.Trim(), returnUrl});
        }

        // ---- password reset

        [HttpGet]
        public IActionResult ForgotPassword(string? returnUrl) => View(new ForgotPasswordModel {ReturnUrl = returnUrl});

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("account")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordModel model)
        {
            var returnUrl = SafeReturnUrl(model.ReturnUrl);
            await _accounts.ForgotPasswordAsync(model.Email,
                token => AbsoluteUrl(nameof(ResetPassword), "Registration", new {token, returnUrl}));

            // the same answer, whether the address has an account or not
            model.Sent = true;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string? token, string? returnUrl)
        {
            if (!await _accounts.IsTokenValidAsync(token, TokenPurpose.ResetPassword))
                return View("LinkExpired", new LinkExpiredModel {Resend = false, ReturnUrl = returnUrl});

            return View(new ResetPasswordModel {Token = token, ReturnUrl = returnUrl});
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("account")]
        public async Task<IActionResult> ResetPassword(ResetPasswordModel model)
        {
            var errors = await _accounts.ResetPasswordAsync(model.Token, model.Password);
            if (errors.Count > 0)
            {
                foreach (var error in errors) ModelState.AddModelError(string.Empty, error);
                model.Password = null;
                return View(model);
            }

            TempData["Notice"] = "Your password was changed. You can sign in with it now.";
            return RedirectToAction("Login", "Account", new {returnUrl = SafeReturnUrl(model.ReturnUrl)});
        }

        private string? SafeReturnUrl(string? returnUrl) => IsValidReturnUrl(returnUrl) ? returnUrl : null;
    }

    public class RegisterModel
    {
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Display(Name = "Your name in meetings")]
        public string? DisplayName { get; set; }

        [Display(Name = "Password")]
        public string? Password { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class CheckEmailModel
    {
        public string? Email { get; set; }
        public string? ReturnUrl { get; set; }
    }

    public class ConfirmEmailModel
    {
        public string? Token { get; set; }
        public string? ReturnUrl { get; set; }
    }

    public class ForgotPasswordModel
    {
        [Display(Name = "Email")]
        public string? Email { get; set; }

        public string? ReturnUrl { get; set; }
        public bool Sent { get; set; }
    }

    public class ResetPasswordModel
    {
        public string? Token { get; set; }

        [Display(Name = "New password")]
        public string? Password { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class LinkExpiredModel
    {
        public bool Resend { get; set; }
        public string? ReturnUrl { get; set; }
    }
}
