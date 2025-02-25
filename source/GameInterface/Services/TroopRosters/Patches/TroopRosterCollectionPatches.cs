using System.Collections.Generic;
using System.Reflection;
using Common.Logging;
using GameInterface.Services.TroopRosters.Messages;
using GameInterface.Utils;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Patches
{
    [HarmonyPatch]
    internal class TroopRosterCollectionPatches : GenericCollectionPatches<TroopRosterCollectionPatches, TroopRoster>
    {
        static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(TroopRoster));

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => ArrayTranspiler<TroopRosterElement, TroopRosterDataUpdated>(instructions, nameof(TroopRoster.data));
    }
}