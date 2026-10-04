#nullable enable
using System;
using System.Threading.Tasks;
using Duende.IdentityServer;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Identity.API.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Identity.API.Quickstart.Account
{
    /// <summary>What every page of the sign in flows needs: finish a sign in and send the person back to the app.</summary>
    public abstract class SignInControllerBase : Controller
    {
        protected SignInControllerBase(IIdentityServerInteractionService interaction, IEventService events,
            IOptions<AccountsOptions> options)
        {
            Interaction = interaction;
            Events = events;
            Options = options.Value;
        }

        protected IIdentityServerInteractionService Interaction { get; }
        protected IEventService Events { get; }
        protected AccountsOptions Options { get; }

        /// <summary>Issue the session cookie and continue with the request of the app.</summary>
        protected async Task<IActionResult> CompleteSignInAsync(string subjectId, string displayName, string loginName,
            string? returnUrl, bool rememberLogin = false)
        {
            var context = await Interaction.GetAuthorizationContextAsync(returnUrl);
            await Events.RaiseAsync(new UserLoginSuccessEvent(loginName, subjectId, displayName,
                clientId: context?.Client.ClientId));

            // only set an explicit expiration if the user chooses "remember me", else the cookie middleware decides
            AuthenticationProperties? props = null;
            if (AccountOptions.AllowRememberLogin && rememberLogin)
                props = new AuthenticationProperties
                {
                    IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.Add(AccountOptions.RememberMeLoginDuration),
                };

            await HttpContext.SignInAsync(new IdentityServerUser(subjectId) {DisplayName = displayName}, props);

            if (context != null)
            {
                // the client is native: a loading page is a better experience than a redirect
                if (context.IsNativeClient()) return this.LoadingPage("Redirect", returnUrl!);

                // we can trust returnUrl since GetAuthorizationContextAsync returned non-null
                return Redirect(returnUrl!);
            }

            // request for a local page
            if (Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl!);
            if (string.IsNullOrEmpty(returnUrl)) return Redirect("~/");
            throw new Exception("invalid return URL");
        }

        /// <summary>Only return urls that IdentityServer or this site made are followed.</summary>
        protected bool IsValidReturnUrl(string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) && (Interaction.IsValidReturnUrl(returnUrl) || Url.IsLocalUrl(returnUrl));

        protected string AbsoluteUrl(string action, string controller, object? values = null) =>
            Url.Action(action, controller, values, Request.Scheme)!;
    }
}
