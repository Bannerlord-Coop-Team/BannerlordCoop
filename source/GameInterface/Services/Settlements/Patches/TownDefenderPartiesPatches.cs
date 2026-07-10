using Common;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// [Client] Null-tolerant Town.GetDefenderParties/GetNextDefenderParty. Vanilla dereferences every
/// settlement party's MapFaction against SiegeEvent.BesiegerCamp.MapFaction with no guards, and
/// these run inside per-frame siege menu paths and the mission spawn iteration — one
/// half-replicated party (no faction yet) or a not-yet-complete siege graph throws every frame and
/// freezes the client. Behavior-identical on a healthy graph.
/// </summary>
[HarmonyPatch]
internal class TownDefenderPartiesPatches
{
    [HarmonyPatch(typeof(Town), nameof(Town.GetDefenderParties))]
    [HarmonyPrefix]
    private static bool Prefix(Town __instance, MapEvent.BattleTypes battleType, ref IEnumerable<PartyBase> __result)
    {
        if (ModInformation.IsServer) return true;

        __result = GetDefenderPartiesSafe(__instance, battleType);
        return false;
    }

    [HarmonyPatch(typeof(Town), nameof(Town.GetNextDefenderParty))]
    [HarmonyPrefix]
    private static bool GetNextPrefix(Town __instance, ref int partyIndex, MapEvent.BattleTypes battleType, ref PartyBase __result)
    {
        if (ModInformation.IsServer) return true;

        __result = GetNextDefenderPartySafe(__instance, ref partyIndex, battleType);
        return false;
    }

    private static PartyBase GetNextDefenderPartySafe(Town town, ref int partyIndex, MapEvent.BattleTypes battleType)
    {
        partyIndex++;
        if (partyIndex == 0) return town.Settlement.Party;

        var besiegerFaction = town.Settlement.SiegeEvent?.BesiegerCamp?.MapFaction;
        if (besiegerFaction == null) return null;

        for (int i = partyIndex - 1; i < town.Settlement.Parties.Count; i++)
        {
            var party = town.Settlement.Parties[i];
            if (party.MapFaction?.IsAtWarWith(besiegerFaction) == true
                && party.IsActive && !party.IsVillager && !party.IsCaravan
                && (!party.IsMilitia || (!town.InRebelliousState && battleType != MapEvent.BattleTypes.SallyOut)))
            {
                partyIndex = i + 1;
                return party.Party;
            }
        }

        return null;
    }

    private static IEnumerable<PartyBase> GetDefenderPartiesSafe(Town town, MapEvent.BattleTypes battleType)
    {
        yield return town.Settlement.Party;

        var besiegerFaction = town.Settlement.SiegeEvent?.BesiegerCamp?.MapFaction;
        if (besiegerFaction == null) yield break;

        foreach (var party in town.Settlement.Parties)
        {
            if (party.MapFaction?.IsAtWarWith(besiegerFaction) == true
                && party.IsActive && !party.IsVillager && !party.IsCaravan
                && (!party.IsMilitia || (!town.InRebelliousState && battleType != MapEvent.BattleTypes.SallyOut)))
            {
                yield return party.Party;
            }
        }
    }
}
