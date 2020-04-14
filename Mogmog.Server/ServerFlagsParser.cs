using Microsoft.Extensions.Configuration;
using System;

namespace Mogmog.Server
{
    public static class ServerFlagsParser
    {
        public static ServerFlags ParseFromConfig(IConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            ServerFlags flags = ServerFlags.None;
            var enumNames = Enum.GetNames(typeof(ServerFlags));
            foreach (var value in enumNames)
            {
                if (config.GetValue<bool>(value))
                    flags |= (ServerFlags)Enum.Parse(typeof(ServerFlags), value);
            }
            return flags;
        }
    }
}
