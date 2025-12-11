using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;

namespace GameInterface.Services.Time.Patches
{
    /// <summary>
    /// Enables campaign ticks while in menu states to prevent local pausing and desynchronisation.
    /// </summary>
    [HarmonyPatch(typeof(MapState), "OnMapModeTick")]
    class MapStatePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions;
        }
    }
}
