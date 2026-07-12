using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Party.Patches;

/// <summary>
/// Debug-only: for parties flagged by coop.debug.mobileparty.siege_buff, forces a high map speed and a
/// huge party size limit so the added troops don't desert back down to the normal cap. Both postfixes
/// are no-ops unless the cheat has flagged a party (the set is empty in normal play) and only run where
/// the flag was set (the server).
/// </summary>
[HarmonyPatch]
internal static class PartyDebugBuffPatches
{
    private const float BoostedSpeed = 30f;
    private const float BoostedSizeLimit = 5000f;

    private static readonly HashSet<MobileParty> boostedParties = new HashSet<MobileParty>();

    public static void Boost(MobileParty party) => boostedParties.Add(party);

    [HarmonyPatch(typeof(DefaultPartySpeedCalculatingModel), nameof(DefaultPartySpeedCalculatingModel.CalculateFinalSpeed))]
    [HarmonyPostfix]
    private static void SpeedPostfix(MobileParty mobileParty, ref ExplainedNumber __result)
    {
        if (boostedParties.Contains(mobileParty))
        {
            __result = new ExplainedNumber(BoostedSpeed);
        }
    }

    [HarmonyPatch(typeof(DefaultPartySizeLimitModel), nameof(DefaultPartySizeLimitModel.GetPartyMemberSizeLimit))]
    [HarmonyPostfix]
    private static void SizeLimitPostfix(PartyBase party, ref ExplainedNumber __result)
    {
        if (party.MobileParty != null && boostedParties.Contains(party.MobileParty))
        {
            __result = new ExplainedNumber(BoostedSizeLimit);
        }
    }
}
