using Common;
using GameInterface.Extentions;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(PartyRolesCampaignBehavior))]
internal class DisablePartyRolesCampaignBehavior
{
    [HarmonyPatch(nameof(PartyRolesCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(PartyRolesCampaignBehavior))]
internal class PartyRolesCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PartyRolesCampaignBehavior.OnHeroKilled))]
    [HarmonyPrefix]
    public static bool OnHeroKilledPrefix(PartyRolesCampaignBehavior __instance, Hero victim)
    {
        if (victim.Clan == null || !victim.Clan.IsPlayerClan()) return false;

        __instance.RemoveAllPartyRolesOfHeroIfExist(victim);

        return false;
    }

    [HarmonyPatch(nameof(PartyRolesCampaignBehavior.OnHeroPrisonerTaken))]
    [HarmonyPrefix]
    public static bool OnHeroPrisonerTakenPrefix(PartyRolesCampaignBehavior __instance, PartyBase party, Hero prisoner)
    {
        if (prisoner.Clan == null || !prisoner.Clan.IsPlayerClan()) return false;

        __instance.RemoveAllPartyRolesOfHeroIfExist(prisoner);

        return false;
    }

    [HarmonyPatch(nameof(PartyRolesCampaignBehavior.OnGovernorChanged))]
    [HarmonyPrefix]
    public static bool OnGovernorChangedPrefix(PartyRolesCampaignBehavior __instance, Town fortification, Hero oldGovernor, Hero newGovernor)
    {
        if (newGovernor == null || newGovernor.Clan == null || !newGovernor.Clan.IsPlayerClan()) return false;

        __instance.RemoveAllPartyRolesOfHeroIfExist(newGovernor);

        return false;
    }

    [HarmonyPatch(nameof(PartyRolesCampaignBehavior.OnPartySpawned))]
    [HarmonyPrefix]
    public static bool OnPartySpawnedPrefix(PartyRolesCampaignBehavior __instance, MobileParty spawnedParty)
    {
        if (spawnedParty.IsLordParty && spawnedParty.ActualClan != null && spawnedParty.ActualClan.IsPlayerClan())
        {
            foreach (TroopRosterElement troopRosterElement in spawnedParty.MemberRoster.GetTroopRoster())
            {
                if (troopRosterElement.Character.IsHero)
                {
                    __instance.RemoveAllPartyRolesOfHeroIfExist(troopRosterElement.Character.HeroObject);
                }
            }
        }

        return false;
    }

    [HarmonyPatch(nameof(PartyRolesCampaignBehavior.OnCompanionRemoved))]
    [HarmonyPrefix]
    public static bool OnCompanionRemovedPrefix(PartyRolesCampaignBehavior __instance, Hero companion)
    {
        __instance.RemoveAllPartyRolesOfHeroIfExist(companion);

        return false;
    }

    [HarmonyPatch(nameof(PartyRolesCampaignBehavior.OnHeroGetsBusy))]
    [HarmonyPrefix]
    public static bool OnHeroGetsBusyPrefix(PartyRolesCampaignBehavior __instance, Hero hero)
    {
        if (hero.Clan == null || !hero.Clan.IsPlayerClan()) return false;

        __instance.RemoveAllPartyRolesOfHeroIfExist(hero);

        return false;
    }

    [HarmonyPatch(nameof(PartyRolesCampaignBehavior.OnHeroChangedClan))]
    [HarmonyPrefix]
    public static bool OnHeroChangedClanPrefix(PartyRolesCampaignBehavior __instance, Hero hero, Clan oldClan)
    {
        if (oldClan == null || !oldClan.IsPlayerClan()) return false;

        __instance.RemoveAllPartyRolesOfHeroIfExist(hero);

        return false;
    }

    [HarmonyPatch(nameof(PartyRolesCampaignBehavior.RemoveAllPartyRolesOfHeroIfExist))]
    [HarmonyPrefix]
    public static bool RemoveAllPartyRolesOfHeroIfExistPrefix(PartyRolesCampaignBehavior __instance, Hero hero)
    {
        // Update for all player clans. Can't use hero.Clan as it could have changed already when this runs
        foreach (var clan in Campaign.Current.CampaignObjectManager.GetPlayerClans())
        {
            foreach (WarPartyComponent warPartyComponent in clan.WarPartyComponents)
            {
                warPartyComponent.MobileParty.RemoveAllPartyRolesOfHero(hero);
            }
        }

        return false;
    }
}