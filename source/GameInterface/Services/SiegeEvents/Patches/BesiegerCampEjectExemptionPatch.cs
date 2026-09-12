using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Vanilla's besieger eject check exempts the human player via IsMainParty. A dedicated host has no
/// MainParty, so a client's besieging party would be ejected the moment its non-besiege DefaultBehavior
/// syncs in from the client (the party's behavior authority), ending the siege and disorganizing the
/// party. Re-run the vanilla check with every co-op player party exempted as well.
/// </summary>
[HarmonyPatch(typeof(BesiegerCamp), nameof(BesiegerCamp.CheckBesiegerPartiesAndMakeThemLeave))]
internal static class BesiegerCampEjectExemptionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(BesiegerCamp __instance)
    {
        var parties = __instance._besiegerParties;
        for (int i = parties.Count - 1; i >= 0; i--)
        {
            var party = parties[i];
            if (party.IsMainParty) continue;
            if (party.LeaderHero != null && party.LeaderHero.IsPlayerHero()) continue;

            if (party.AttachedTo == null
                && party.DefaultBehavior != AiBehavior.BesiegeSettlement
                && party.DefaultBehavior != AiBehavior.EscortParty
                && party.DefaultBehavior != AiBehavior.AssaultSettlement)
            {
                party.BesiegerCamp = null;
            }
        }

        return false;
    }
}
