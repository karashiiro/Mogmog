using System;

namespace Mogmog.FFXIV.UpgradeLayer
{
    [Serializable]
    public class MogmogConfiguration
    {
        public int Version { get; set; }

        public StrongIndexedList<string> Hostnames;

        public MogmogConfiguration()
        {
            Hostnames = new StrongIndexedList<string>();
        }
    }
}
