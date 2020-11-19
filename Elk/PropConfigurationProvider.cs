using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Elk
{
    internal class PropConfigurationProvider : FileConfigurationProvider
    {

        public PropConfigurationProvider(PropConfigurationSource source) : base(source)
        {
        }

        public override void Load(Stream stream)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();
            using (StreamReader sr = new StreamReader(stream))
            {
                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    int index = line.IndexOf(" = ");
                    if (index > 0)
                    {
                        var key = line.Substring(0, index).Trim();
                        var value = line.Substring(index + 3);
                        byte[] buf = Convert.FromBase64String(value);
                        data[key] = System.Text.Encoding.Default.GetString(buf);
                    }
                }

            }

            this.Data = data;
        }
    }
}
