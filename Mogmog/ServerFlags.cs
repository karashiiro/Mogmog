using System;

namespace Mogmog
{
    [Flags]
    public enum ServerFlags : uint
    {
        None = 0,
        RequiresDiscordOAuth2 = 1,
        Field2 = 2,
        Field3 = 4,
        Field4 = 8,
        Field5 = 16,
        Field6 = 32,
        Field7 = 64,
        Field8 = 128,
        Field9 = 256,
        Field10 = 512,
        Field11 = 1024,
        Field12 = 2048,
        Field13 = 4096,
        Field14 = 8192,
        Field15 = 16384,
        Field16 = 32768,
        Field17 = 65536,
        Field18 = 131072,
        Field19 = 262144,
        Field20 = 524288,
        Field21 = 1048576,
        Field22 = 2097152,
        Field23 = 4194304,
        Field24 = 8388608,
        Field25 = 16777216,
        Field26 = 33554432,
        Field27 = 67108864,
        Field28 = 134217728,
        Field29 = 268435456,
        Field30 = 536870912,
        Field31 = 1073741824,
        Field32 = 2147483648,
    }
}
