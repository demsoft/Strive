#nullable enable
using System;
using Identity.API.Accounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.API.Quickstart
{
    /// <summary>
    ///     The pages for registration and external sign in do not exist in demo mode. A resource filter, because it
    ///     must run before the controller is created (the services it needs are not registered in demo mode).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AccountsModeOnlyAttribute : Attribute, IResourceFilter
    {
        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<AccountsOptions>>().Value;
            if (!options.IsAccountsMode) context.Result = new NotFoundResult();
        }

        public void OnResourceExecuted(ResourceExecutedContext context)
        {
        }
    }
}
