using System;
using System.Collections.Generic;

namespace Mogmog.FFXIV.UpgradeLayer
{
    [Serializable]
    public class Host
    {
        public string Hostname { get; set; }
        public bool SaveAccessCode { get; set; }
    }

    [Serializable]
    public class MogmogConfiguration
    {
        public int Version { get; set; }

        public IList<Host> Hosts { get; private set; }

        public IList<UserFragment> BlockedUsers { get; private set; }

        public MogmogConfiguration()
        {
            Hosts = new StrongIndexedList<Host>();
            BlockedUsers = new List<UserFragment>();
        }
    }
}
