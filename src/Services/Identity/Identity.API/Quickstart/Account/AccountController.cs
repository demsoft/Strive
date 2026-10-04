// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Threading.Tasks;
using System;
using Duende.IdentityModel;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Identity.API.Accounts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.API.Quickstart.Account
{
    /// <summary>
    ///     This sample controller implements a typical login/logout/provision workflow for local and external accounts.
    ///     The login service encapsulates the interactions with the user data store. This data store is in-memory only and
    ///     cannot be used for production!
    ///     The interaction service provides a way for the UI to communicate with identityserver for validation and context
    ///     retrieval
    /// </summary>
    [SecurityHeaders]
    [AllowAnonymous]
    public class AccountController : SignInControllerBase
    {
        private readonly AccountService _accounts;
        private readonly IClientStore _clientStore;
        private readonly IAuthenticationSchemeProvider _schemeProvider;
        private readonly IUserProvider _users;

        public AccountController(
            IIdentityServerInteractionService interaction,
            IClientStore clientStore,
            IAuthenticationSchemeProvider schemeProvider,
            IEventService events,
            IUserProvider users,
            IOptions<AccountsOptions> options,
            // only registered in accounts mode
            AccountService accounts = null) : base(interaction, events, options)
        {
            _accounts = accounts;
            // if the TestUserStore is not in DI, then we'll just use the global users collection
            // this is where you would plug in your own custom identity management library (e.g. ASP.NET Identity)
            _users = users;

            _clientStore = clientStore;
            _schemeProvider = schemeProvider;
        }

        /// <summary>
        ///     Entry point into the login workflow
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Login(string returnUrl)
        {
            // build a model so we know what to show on the login page
            var vm = await BuildLoginViewModelAsync(returnUrl);

            return View(vm);
        }

        /// <summary>
        ///     Handle postback from username/password login
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("account")]
        public async Task<IActionResult> Login(LoginInputModel model, string button)
        {
            // check if we are in the context of an authorization request
            var context = await Interaction.GetAuthorizationContextAsync(model.ReturnUrl);

            // the user clicked the "cancel" button
            if (button != "login")
            {
                if (context != null)
                {
                    // if the user cancels, send a result back into IdentityServer as if they 
                    // denied the consent (even if this client does not require consent).
                    // this will send back an access denied OIDC error response to the client.
                    await Interaction.DenyAuthorizationAsync(context, AuthorizationError.AccessDenied);

                    // we can trust model.ReturnUrl since GetAuthorizationContextAsync returned non-null
                    if (context.IsNativeClient())
                        // The client is native, so this change in how to
                        // return the response is for better UX for the end user.
                        return this.LoadingPage("Redirect", model.ReturnUrl);

                    return Redirect(model.ReturnUrl);
                }

                // since we don't have a valid context, then we just go back to the home page
                return Redirect(AppHome);
            }

            if (ModelState.IsValid)
            {
                if (Options.IsAccountsMode)
                {
                    var outcome = await _accounts.PasswordSignInAsync(model.Username, model.Password);
                    switch (outcome.Status)
                    {
                        case SignInStatus.Succeeded:
                            return await CompleteSignInAsync(outcome.User!.Id, outcome.User.DisplayName ?? outcome.User.Email,
                                outcome.User.Email, model.ReturnUrl, model.RememberLogin);
                        case SignInStatus.LockedOut:
                            await Events.RaiseAsync(new UserLoginFailureEvent(model.Username, "locked out",
                                clientId: context?.Client.ClientId));
                            ModelState.AddModelError(string.Empty,
                                "Too many failed attempts. Try again in a few minutes or reset your password.");
                            break;
                        case SignInStatus.EmailNotConfirmed:
                            ModelState.AddModelError(string.Empty,
                                "Please confirm your email address first. We sent you a link when you signed up.");
                            var unconfirmed = await BuildLoginViewModelAsync(model);
                            unconfirmed.ShowResendConfirmation = true;
                            return View(unconfirmed);
                        default:
                            await Events.RaiseAsync(new UserLoginFailureEvent(model.Username, "invalid credentials",
                                clientId: context?.Client.ClientId));
                            ModelState.AddModelError(string.Empty, AccountOptions.InvalidCredentialsErrorMessage);
                            break;
                    }
                }
                // validate username/password against the demo store
                else if (_users.ValidateCredentials(model.Username, model.Password))
                {
                    var user = _users.FindByUsername(model.Username);
                    return await CompleteSignInAsync(user.SubjectId, user.Username, user.Username, model.ReturnUrl,
                        model.RememberLogin);
                }
                else
                {
                    await Events.RaiseAsync(new UserLoginFailureEvent(model.Username, "invalid credentials",
                        clientId: context?.Client.ClientId));
                    ModelState.AddModelError(string.Empty, AccountOptions.InvalidCredentialsErrorMessage);
                }
            }

            // something went wrong, show form with error
            var vm = await BuildLoginViewModelAsync(model);
            return View(vm);
        }


        /// <summary>
        ///     Show logout page
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Logout(string logoutId)
        {
            // build a model so the logout page knows what to display
            var vm = await BuildLogoutViewModelAsync(logoutId);

            if (vm.ShowLogoutPrompt == false)
                // if the request for logout was properly authenticated from IdentityServer, then
                // we don't need to show the prompt and can just log the user out directly.
                return await Logout(vm);

            return View(vm);
        }

        /// <summary>
        ///     Handle logout page postback
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(LogoutInputModel model)
        {
            // build a model so the logged out page knows what to display
            var vm = await BuildLoggedOutViewModelAsync(model.LogoutId);

            if (User?.Identity.IsAuthenticated == true)
            {
                // delete local authentication cookie
                await HttpContext.SignOutAsync();

                // raise the logout event
                await Events.RaiseAsync(new UserLogoutSuccessEvent(User.GetSubjectId(), User.GetDisplayName()));
            }

            // check if we need to trigger sign-out at an upstream identity provider
            if (vm.TriggerExternalSignout)
            {
                // build a return URL so the upstream provider will redirect back
                // to us after the user has logged out. this allows us to then
                // complete our single sign-out processing.
                var url = Url.Action("Logout", new {logoutId = vm.LogoutId});

                // this triggers a redirect to the external provider for sign-out
                return SignOut(new AuthenticationProperties {RedirectUri = url}, vm.ExternalAuthenticationScheme);
            }

            return View("LoggedOut", vm);
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }


        /*****************************************/
        /* helper APIs for the AccountController */
        /*****************************************/
        private async Task<LoginViewModel> BuildLoginViewModelAsync(string returnUrl)
        {
            var context = await Interaction.GetAuthorizationContextAsync(returnUrl);
            if (context?.IdP != null && await _schemeProvider.GetSchemeAsync(context.IdP) != null)
            {
                var local = context.IdP == IdentityServerConstants.LocalIdentityProvider;

                // this is meant to short circuit the UI and only trigger the one external IdP
                var vm = new LoginViewModel
                {
                    EnableLocalLogin = local,
                    ReturnUrl = returnUrl,
                    Username = context?.LoginHint,
                    AccountsMode = Options.IsAccountsMode,
                    GoogleEnabled = local == false && Options.Google.IsConfigured,
                };

                return vm;
            }

            var allowLocal = true;
            if (context?.Client.ClientId != null)
            {
                var client = await _clientStore.FindEnabledClientByIdAsync(context.Client.ClientId);
                if (client != null) allowLocal = client.EnableLocalLogin;
            }

            return new LoginViewModel
            {
                AllowRememberLogin = AccountOptions.AllowRememberLogin,
                EnableLocalLogin = allowLocal && AccountOptions.AllowLocalLogin,
                ReturnUrl = returnUrl,
                Username = context?.LoginHint,
                AccountsMode = Options.IsAccountsMode,
                GoogleEnabled = Options.IsAccountsMode && Options.Google.IsConfigured,
                Notice = TempData["Notice"] as string,
                Error = TempData["Error"] as string,
            };
        }

        private async Task<LoginViewModel> BuildLoginViewModelAsync(LoginInputModel model)
        {
            var vm = await BuildLoginViewModelAsync(model.ReturnUrl);
            vm.Username = model.Username;
            vm.RememberLogin = model.RememberLogin;
            return vm;
        }

        private async Task<LogoutViewModel> BuildLogoutViewModelAsync(string logoutId)
        {
            var vm = new LogoutViewModel {LogoutId = logoutId, ShowLogoutPrompt = AccountOptions.ShowLogoutPrompt};

            if (User?.Identity.IsAuthenticated != true)
            {
                // if the user is not authenticated, then just show logged out page
                vm.ShowLogoutPrompt = false;
                return vm;
            }

            var context = await Interaction.GetLogoutContextAsync(logoutId);
            if (context?.ShowSignoutPrompt == false)
            {
                // it's safe to automatically sign-out
                vm.ShowLogoutPrompt = false;
                return vm;
            }

            // show the logout prompt. this prevents attacks where the user
            // is automatically signed out by another malicious web page.
            return vm;
        }

        private async Task<LoggedOutViewModel> BuildLoggedOutViewModelAsync(string logoutId)
        {
            // get context information (client name, post logout redirect URI and iframe for federated signout)
            var logout = await Interaction.GetLogoutContextAsync(logoutId);

            var vm = new LoggedOutViewModel
            {
                AutomaticRedirectAfterSignOut = AccountOptions.AutomaticRedirectAfterSignOut,
                PostLogoutRedirectUri = logout?.PostLogoutRedirectUri,
                ClientName = string.IsNullOrEmpty(logout?.ClientName) ? logout?.ClientId : logout?.ClientName,
                SignOutIframeUrl = logout?.SignOutIFrameUrl,
                LogoutId = logoutId
            };

            if (User?.Identity.IsAuthenticated == true)
            {
                var idp = User.FindFirst(JwtClaimTypes.IdentityProvider)?.Value;
                if (idp != null && idp != IdentityServerConstants.LocalIdentityProvider)
                {
                    var handlerProvider = HttpContext.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
                    var handler = await handlerProvider.GetHandlerAsync(HttpContext, idp);
                    var providerSupportsSignout = handler is IAuthenticationSignOutHandler;
                    if (providerSupportsSignout)
                    {
                        if (vm.LogoutId == null)
                            // if there's no current logout context, we need to create one
                            // this captures necessary info from the current logged in user
                            // before we signout and redirect away to the external IdP for signout
                            vm.LogoutId = await Interaction.CreateLogoutContextAsync();

                        vm.ExternalAuthenticationScheme = idp;
                    }
                }
            }

            return vm;
        }
    }
}