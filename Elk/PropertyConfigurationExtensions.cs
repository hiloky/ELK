using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Elk
{
    public static class PropertyConfigurationExtensions
    {
        public static IConfigurationBuilder AddPropFile(this IConfigurationBuilder builder, string path, bool reloadOnChange)
        {
            return builder.AddPropFile(s =>
            {
                s.FileProvider = null;
                s.Path = path;
                s.Optional = true;
                s.ReloadOnChange = reloadOnChange;
                s.ResolveFileProvider();
            });
        }
        public static IConfigurationBuilder AddPropFile(this IConfigurationBuilder builder, Action<PropConfigurationSource> configureSource)
        {
            return ConfigurationExtensions.Add<PropConfigurationSource>(builder, configureSource);
        }
    }
}
