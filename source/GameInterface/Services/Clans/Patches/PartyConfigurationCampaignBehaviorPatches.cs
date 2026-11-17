using Common;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Clans.Patches;

// The vanilla reset nulls a field that is not synced, so each reset goes through IPartyConfigurationService instead.
[HarmonyPatch(typeof(PartyConfigurationCampaignBehavior))]
internal class PartyConfigurationCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PartyConfigurationCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    private static bool RegisterEventsPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(PartyConfigurationCampaignBehavior.OnCompanionRemoved))]
    [HarmonyPrefix]
    private static bool OnCompanionRemovedPrefix(Hero hero) => ResetHero(hero);

    [HarmonyPatch(nameof(PartyConfigurationCampaignBehavior.OnBeforePlayerCharacterChanged))]
    [HarmonyPrefix]
    private static bool OnBeforePlayerCharacterChangedPrefix(Hero hero) => ResetHero(hero);

    // The event passes the clan the hero left.
    [HarmonyPatch(nameof(PartyConfigurationCampaignBehavior.OnHeroChangedClan))]
    [HarmonyPrefix]
    private static bool OnHeroChangedClanPrefix(Hero hero, Clan clan)
    {
        if (!ContainerProvider.TryResolve<IPartyConfigurationService>(out var partyConfiguration)) return true;

        if (clan.IsPlayerClan())
            partyConfiguration.Reset(hero);
        return false;
    }

    // Runs vanilla's reset of the hero's party configuration through the service, or vanilla itself without one
    private static bool ResetHero(Hero hero)
    {
        if (!ContainerProvider.TryResolve<IPartyConfigurationService>(out var partyConfiguration)) return true;

        if (hero != null) partyConfiguration.Reset(hero);
        return false;
    }
}
