using Common;
using GameInterface.Services.Companions.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Companions.Patches;

[HarmonyPatch(typeof(PlayerTrackCompanionBehavior))]
internal class PlayerTrackCompanionBehaviorPatches
{
    [HarmonyPatch(nameof(PlayerTrackCompanionBehavior.OnHeroTeleportationRequested))]
    [HarmonyPrefix]
    public static bool OnHeroTeleportationRequestedPrefix(Hero hero, Settlement settlement, MobileParty party, TeleportHeroAction.TeleportationDetail detail)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IPlayerTrackCompanionBehaviorInterface>(out var playerTrackCompanionBehaviorInterface)) return true;

        playerTrackCompanionBehaviorInterface.OnHeroTeleportationRequested(hero, party, detail);

        return false;
    }

    [HarmonyPatch(nameof(PlayerTrackCompanionBehavior.OnGameLoadFinished))]
    [HarmonyPrefix]
    public static bool OnGameLoadFinishedPrefix() => false;

    [HarmonyPatch(nameof(PlayerTrackCompanionBehavior.AddHeroToScatteredCompanions))]
    [HarmonyPrefix]
    public static bool AddHeroToScatteredCompanionsPrefix(Hero hero)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IPlayerTrackCompanionBehaviorInterface>(out var playerTrackCompanionBehaviorInterface)) return true;

        playerTrackCompanionBehaviorInterface.AddHeroToScatteredCompanions(hero);

        return false;
    }

    [HarmonyPatch(nameof(PlayerTrackCompanionBehavior.SettlementEntered))]
    [HarmonyPrefix]
    public static bool SettlementEnteredPrefix(MobileParty party, Settlement settlement)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IPlayerTrackCompanionBehaviorInterface>(out var playerTrackCompanionBehaviorInterface)) return true;

        playerTrackCompanionBehaviorInterface.SettlementEntered(party, settlement);

        return false;
    }

    [HarmonyPatch(nameof(PlayerTrackCompanionBehavior.CompanionAdded))]
    [HarmonyPrefix]
    public static bool CompanionAddedPrefix(Hero companion)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IPlayerTrackCompanionBehaviorInterface>(out var playerTrackCompanionBehaviorInterface)) return true;

        playerTrackCompanionBehaviorInterface.CompanionAdded(companion);

        return false;
    }

    [HarmonyPatch(nameof(PlayerTrackCompanionBehavior.CompanionRemoved))]
    [HarmonyPrefix]
    public static bool CompanionRemovedPrefix(Hero companion)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IPlayerTrackCompanionBehaviorInterface>(out var playerTrackCompanionBehaviorInterface)) return true;

        playerTrackCompanionBehaviorInterface.CompanionRemoved(companion);

        return false;
    }

    [HarmonyPatch(nameof(PlayerTrackCompanionBehavior.OnMobilePartyCreated))]
    [HarmonyPrefix]
    public static bool OnMobilePartyCreatedPrefix(MobileParty mobileParty)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IPlayerTrackCompanionBehaviorInterface>(out var playerTrackCompanionBehaviorInterface)) return true;

        playerTrackCompanionBehaviorInterface.OnMobilePartyCreated(mobileParty);

        return false;
    }
}
