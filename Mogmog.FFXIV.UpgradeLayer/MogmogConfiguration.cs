using System;
using System.Collections.Generic;

namespace Mogmog.FFXIV.UpgradeLayer
{
    [Serializable]
    public class MogmogConfiguration
    {
        public int Version { get; set; }

        public IList<string> Hostnames { get; }

        public MogmogConfiguration()
        {
            Hostnames = new StrongIndexedList<string>();
        }
    }
}
