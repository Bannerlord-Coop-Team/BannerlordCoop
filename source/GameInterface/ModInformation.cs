using System;

namespace GameInterface
{
    public static class ModInformation
    {
        public static bool IsServer { get; set; } = false;
        public static bool IsClient => !IsServer;

        public static readonly bool DISABLE_AI = false;

        public static Version Version => new("0.0.1");
    }
}
