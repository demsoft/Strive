using System;
using Microsoft.Extensions.Configuration;

namespace Strive.Extensions
{
    public static class ConfigurationExtensions
    {
        public static T GetRequired<T>(this IConfiguration configuration, string sectionName)
        {
            return configuration.GetSection(sectionName).Get<T>() ??
                   throw new InvalidOperationException($"The configuration section \"{sectionName}\" is missing.");
        }
    }
}
