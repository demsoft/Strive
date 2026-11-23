using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SpaServices.ReactDevelopmentServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using System;

namespace WebSPA
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy());

            services.Configure<AppSettings>(Configuration.GetSection("App"));

            services.AddControllers();

            // In production, the React files will be served from this directory
            services.AddSpaStaticFiles(configuration => { configuration.RootPath = "Client/build"; });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/hrm"))
                {
                    var origins = new[] { Configuration["App:HrmPortalOrigin"], Configuration["App:HrmCandidatePortalOrigin"] };
                    var trusted = new System.Collections.Generic.List<string>();
                    foreach (var origin in origins)
                        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
                            uri.UserInfo.Length == 0 && uri.GetLeftPart(UriPartial.Authority) == origin) trusted.Add(origin);
                    context.Response.Headers["Content-Security-Policy"] = trusted.Count > 0 ? "frame-ancestors 'self' " + string.Join(" ", trusted) : "frame-ancestors 'none'";
                    context.Response.Headers["Referrer-Policy"] = "no-referrer";
                    context.Response.Headers["Cache-Control"] = "no-store";
                }
                await next();
            });

            app.UseStaticFiles();
            app.UseSpaStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health");
            });

            app.UseSpa(spa =>
            {
                spa.Options.SourcePath = "Client";

                if (env.IsDevelopment())
                    // uncomment this if you want the React app to start with ASP.Net Core (else you have to start it manually)
                    spa.UseReactDevelopmentServer("start");
                //spa.UseProxyToSpaDevelopmentServer("http://localhost:3000");
            });
        }
    }
}
