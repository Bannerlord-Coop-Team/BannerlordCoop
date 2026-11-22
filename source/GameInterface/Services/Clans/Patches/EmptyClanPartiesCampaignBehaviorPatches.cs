using Common;
using GameInterface.Services.Clans.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(EmptyClanPartiesCampaignBehavior))]
internal class EmptyClanPartiesCampaignBehaviorPatches
{
    // Clients hold the server's whole list, so only the local player's clan is listed.
    [HarmonyPatch(nameof(EmptyClanPartiesCampaignBehavior.GetEmptyClanPartyLeaders))]
    [HarmonyPrefix]
    public static bool GetEmptyClanPartyLeadersPrefix(EmptyClanPartiesCampaignBehavior __instance, ref MBReadOnlyList<Hero> __result)
    {
        if (ModInformation.IsServer) return true;

        var leaders = new MBList<Hero>();
        foreach (var emptyClanParty in __instance._emptyClanParties)
        {
            if (emptyClanParty.Leader.Clan == Clan.PlayerClan) leaders.Add(emptyClanParty.Leader);
        }

        __result = leaders;
        return false;
    }

    [HarmonyPatch(nameof(EmptyClanPartiesCampaignBehavior.OnMobilePartyDestroyed))]
    [HarmonyPrefix]
    public static bool OnMobilePartyDestroyedPrefix(MobileParty party, PartyBase destroyerParty)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IEmptyClanPartiesCampaignBehaviorInterface>(out var emptyClanParties)) return true;

        emptyClanParties.OnMobilePartyDestroyed(party, destroyerParty);
        return false;
    }

    [HarmonyPatch(nameof(EmptyClanPartiesCampaignBehavior.OnBeforeHeroKilled))]
    [HarmonyPrefix]
    public static bool OnBeforeHeroKilledPrefix(Hero victim)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IEmptyClanPartiesCampaignBehaviorInterface>(out var emptyClanParties)) return true;

        emptyClanParties.OnBeforeHeroKilled(victim);
        return false;
    }

    [HarmonyPatch(nameof(EmptyClanPartiesCampaignBehavior.OnMobilePartyCreated))]
    [HarmonyPrefix]
    public static bool OnMobilePartyCreatedPrefix(MobileParty party)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IEmptyClanPartiesCampaignBehaviorInterface>(out var emptyClanParties)) return true;

        emptyClanParties.OnMobilePartyCreated(party);
        return false;
    }

    // Shared by OnSettlementEntered and OnHeroPrisonerReleased.
    [HarmonyPatch(nameof(EmptyClanPartiesCampaignBehavior.TryCreateNewClanMobilePartyFromCachedEmptyParties))]
    [HarmonyPrefix]
    public static bool TryCreateNewClanMobilePartyFromCachedEmptyPartiesPrefix(Hero hero, Settlement settlement)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IEmptyClanPartiesCampaignBehaviorInterface>(out var emptyClanParties)) return true;

        emptyClanParties.TryCreateNewClanMobilePartyFromCachedEmptyParties(hero, settlement);
        return false;
    }
}
