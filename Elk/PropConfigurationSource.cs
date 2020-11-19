using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Elk
{
    public class PropConfigurationSource : FileConfigurationSource
    {
        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            base.EnsureDefaults(builder);
            return new PropConfigurationProvider(this);
        }
    }
}
