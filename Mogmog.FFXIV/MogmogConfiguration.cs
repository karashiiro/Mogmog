using Dalamud.Configuration;
using System;

namespace Mogmog.FFXIV
{
    [Serializable]
    public class MogmogConfiguration : IPluginConfiguration
    {
        public int Version { get; set; }

        public string Hostname;
    }
}
