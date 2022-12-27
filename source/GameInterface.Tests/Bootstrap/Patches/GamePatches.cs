using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;

namespace GameInterface.Tests.Bootstrap.Patches
{
    [HarmonyPatch(typeof(Game), "InitializeParameters")]
    internal class GamePatches
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            return false;
        }
    }
}
