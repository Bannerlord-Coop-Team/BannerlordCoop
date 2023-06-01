using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface
{
    public static class ModInformation
    {
        public static bool IsServer = false;
        public static bool IsClient => !IsServer;
    }
}
