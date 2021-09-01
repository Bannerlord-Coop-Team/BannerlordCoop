using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Patch.Hack
{
    /// <summary>
    /// In 1.6, TaleWorlds added a "IsTemporary" flag to objects registered within the MBObjectManager class, which are then removed later.
    /// This breaks our use of MBGUIDS to reference Mobileparties/Heroes/etc. We're just patching that out so we can still reference the objects.
    /// </summary>
    [HarmonyPatch(typeof(MBObjectManager), "RemoveTemporaryTypes")]
    internal class MBObjectManagerPatch
    {
        private static bool Prefix()
        {
            return false;
        }
    }
}
