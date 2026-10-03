// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Duende.IdentityServer;
using Identity.API.Accounts;
using Identity.API.Quickstart;
using Identity.API.Quickstart.Account;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Identity.API
{
    /// <summary>Creates the indexes of the user store.</summary>
    public class AccountsStartup : IHostedService
    {
        private readonly MongoUserRepository _users;

        public AccountsStartup(MongoUserRepository users)
        {
            _users = users;
        }

        public Task StartAsync(CancellationToken cancellationToken) => _users.EnsureIndexesAsync();

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class Startup
    {
        public Startup(IWebHostEnvironment environment, IConfiguration configuration)
        {
            Environment = environment;
            Configuration = configuration;
        }

        public IWebHostEnvironment Environment { get; }
        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var identityConfig = Configuration.GetSection("IdentityServer");
            var spaHost = identityConfig["SpaClientHost"];
            var issuerUri = identityConfig["Issuer"];

            services.AddControllersWithViews().AddRazorRuntimeCompilation();

            var builder = services.AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;

                options.IssuerUri = issuerUri;

                // see https://docs.duendesoftware.com/identityserver/v5/fundamentals/resources/
                options.EmitStaticAudienceClaim = true;
            });

            services.AddSingleton<IUserProvider, DemoUserProvider>();
            ConfigureAccounts(services);

            // in-memory, code config
            builder.AddInMemoryIdentityResources(Config.IdentityResources);
            builder.AddInMemoryClients(new[] {Config.BuildSpaClient(spaHost)});
            builder.AddProfileService<ProfileService>();

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                // ref: https://github.com/aspnet/Docs/issues/2384

                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.RequireHeaderSymmetry = false;

                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                // sign in, registration and password reset: enough for a person, too little for guessing
                options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
                    }));
            });

            services.AddCors(x =>
                x.AddDefaultPolicy(builder => builder.WithOrigins(spaHost).AllowAnyMethod().AllowAnyHeader()));
        }

        /// <summary>
        ///     Demo mode needs nothing. Accounts mode adds the user store (MongoDB), the emails, Google and persistent
        ///     data protection keys.
        /// </summary>
        private void ConfigureAccounts(IServiceCollection services)
        {
            var section = Configuration.GetSection(AccountsOptions.Section);
            services.Configure<AccountsOptions>(section);
            var options = section.Get<AccountsOptions>() ?? new AccountsOptions();

            if (!options.IsAccountsMode)
            {
                services.AddSingleton<IUserDirectory, DemoUserDirectory>();
                return;
            }

            services.AddSingleton<MongoUserRepository>();
            services.AddSingleton<IUserRepository>(sp => sp.GetRequiredService<MongoUserRepository>());
            services.AddSingleton<IUserDirectory, AccountsUserDirectory>();
            // without an SMTP server nothing is sent (the mails are only logged), password reset does not work then
            if (string.IsNullOrWhiteSpace(options.Email.Host)) services.AddSingleton<IEmailSender, LoggingEmailSender>();
            else services.AddSingleton<IEmailSender, SmtpEmailSender>();
            services.AddSingleton<AccountService>();
            services.AddHostedService<AccountsStartup>();

            // cookies, anti-forgery tokens and the Google sign in state must survive restarts
            services.AddDataProtection().SetApplicationName("strive-identity");
            services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
                new ConfigureOptions<KeyManagementOptions>(keys =>
                    keys.XmlRepository = new MongoXmlRepository(sp.GetRequiredService<MongoUserRepository>().Database)));

            if (options.Google.IsConfigured)
                services.AddAuthentication().AddGoogle(ExternalController.GoogleProvider, google =>
                {
                    google.ClientId = options.Google.ClientId!;
                    google.ClientSecret = options.Google.ClientSecret!;
                    google.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                    // Google tells whether it checked the address, without it nobody may be linked by email
                    google.ClaimActions.MapJsonKey("email_verified", "email_verified");
                });
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseForwardedHeaders();

            if (Environment.IsDevelopment()) app.UseDeveloperExceptionPage();

            app.UseCors();

            app.UseCookiePolicy(new CookiePolicyOptions
            {
                MinimumSameSitePolicy = SameSiteMode.None, Secure = CookieSecurePolicy.Always,
            });

            app.UseStaticFiles();

            app.UseRouting();
            app.UseRateLimiter();
            app.UseIdentityServer();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => { endpoints.MapDefaultControllerRoute(); });
        }
    }
}