using System;
using System.Collections.Generic;

namespace Mogmog.FFXIV
{
    [Serializable]
    public class MogmogConfiguration
    {
        public int Version { get; set; }

        public IList<string> Hostnames;

        public MogmogConfiguration()
        {
            Hostnames = new List<string>();
        }
    }
}
