using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace Mogmog.FFXIV
{
    [Serializable]
    public class MogmogConfiguration : IPluginConfiguration
    {
        public int Version { get; set; }

        public IList<string> Hostnames { get; private set; }

        public MogmogConfiguration()
        {
            Hostnames = new StrongIndexedList<string>();
        }
    }
}
