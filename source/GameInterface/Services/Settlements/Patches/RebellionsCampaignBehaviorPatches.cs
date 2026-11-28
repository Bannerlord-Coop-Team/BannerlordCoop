using Common;
using GameInterface.Utils;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Settlements.Patches;


[HarmonyPatch(typeof(RebellionsCampaignBehavior))]
internal class RebellionsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(RebellionsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;

    // Vanilla doesnt execute a rebel held by the main party, which on the server is never a player party
    [HarmonyPatch(nameof(RebellionsCampaignBehavior.DailyTickClan))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> DailyTickClanTranspiler(IEnumerable<CodeInstruction> instructions)
        => MainPartyComparisonTranspiler.ReplaceMainPartyComparisonsWithIsPlayerParty(instructions);
}
