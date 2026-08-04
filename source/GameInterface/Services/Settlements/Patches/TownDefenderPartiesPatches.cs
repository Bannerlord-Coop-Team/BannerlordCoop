using Common;
using GameInterface.Configuration;
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
        if (ModInformation.IsServer && !MilitiaOptInApplies(battleType)) return true;

        __result = GetDefenderPartiesSafe(__instance, battleType);
        return false;
    }

    [HarmonyPatch(typeof(Town), nameof(Town.GetNextDefenderParty))]
    [HarmonyPrefix]
    private static bool GetNextPrefix(Town __instance, ref int partyIndex, MapEvent.BattleTypes battleType, ref PartyBase __result)
    {
        if (ModInformation.IsServer && !MilitiaOptInApplies(battleType)) return true;

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
                && MilitiaMayFight(town, party, battleType))
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
                && MilitiaMayFight(town, party, battleType))
            {
                yield return party.Party;
            }
        }
    }

    /// <summary>
    /// Vanilla keeps militia off a sally-out - they hold the walls while the garrison sorties - which also
    /// keeps them out of the strength comparison that decides whether to sortie at all. A town with thousands
    /// of militia and a small garrison therefore never sorties, which reads as broken even though it is
    /// faithful. <see cref="ModConfigProvider.ModOptions.MilitiaJoinsSallyOut"/> opts into letting them join.
    /// Rebellious towns still hold their militia back, exactly as vanilla does.
    /// </summary>
    /// <summary>
    /// True when the militia opt-in would change who defends, which is the only reason the server takes
    /// over these methods - the sortie decision (CheckSallyOut) runs server-side and reads the same roster,
    /// so a client-only patch could never affect it.
    /// </summary>
    private static bool MilitiaOptInApplies(MapEvent.BattleTypes battleType)
        => battleType == MapEvent.BattleTypes.SallyOut && ModConfigProvider.ModOptions.MilitiaJoinsSallyOut;

    private static bool MilitiaMayFight(Town town, MobileParty party, MapEvent.BattleTypes battleType)
    {
        if (!party.IsMilitia) return true;
        if (town.InRebelliousState) return false;
        if (battleType != MapEvent.BattleTypes.SallyOut) return true;

        return ModConfigProvider.ModOptions.MilitiaJoinsSallyOut;
    }

}
