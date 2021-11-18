using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Patch
{
    [HarmonyPatch(typeof(MBObjectManager), "RemoveTemporaryTypes")]
    class TemporaryTypesPatch
    {
        static bool Prefix()
        {
            return false;
        }        
    }
}
