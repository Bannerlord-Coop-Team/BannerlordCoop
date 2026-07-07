using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Debug-only: for a party pinned by coop.debug.siege.start, skips AiMilitaryBehavior's hourly military
/// think so the campaign AI never re-tasks it off the forced siege. Without this the think re-scores the
/// lord, flips its DefaultBehavior off BesiegeSettlement, and the besieger camp ejects it. The siege
/// still advances to an assault because that is driven by the siege/encounter system, not this think.
/// No-op unless the cheat has pinned a party.
/// </summary>
[HarmonyPatch(typeof(AiMilitaryBehavior), "AiHourlyTick")]
internal static class SiegeDebugPinPatch
{
    private static readonly HashSet<MobileParty> pinnedParties = new HashSet<MobileParty>();

    public static void Pin(MobileParty party) => pinnedParties.Add(party);

    [HarmonyPrefix]
    private static bool Prefix(MobileParty mobileParty) => !pinnedParties.Contains(mobileParty);
}
