using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// The behavior runs on the server only, and its reset nulls a field that is not
/// synced, so each reset goes through <see cref="IPartyConfigurationService.Reset"/> instead.
/// </summary>
[HarmonyPatch(typeof(PartyConfigurationCampaignBehavior))]
internal class PartyConfigurationCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PartyConfigurationCampaignBehavior.OnCompanionRemoved))]
    [HarmonyPrefix]
    private static bool OnCompanionRemovedPrefix(Hero hero)
    {
        if (!ContainerProvider.TryResolve<IPartyConfigurationService>(out var partyConfiguration)) return true;

        if (hero != null) partyConfiguration.Reset(hero);
        return false;
    }

    [HarmonyPatch(nameof(PartyConfigurationCampaignBehavior.OnBeforePlayerCharacterChanged))]
    [HarmonyPrefix]
    private static bool OnBeforePlayerCharacterChangedPrefix(Hero hero)
    {
        if (!ContainerProvider.TryResolve<IPartyConfigurationService>(out var partyConfiguration)) return true;

        if (hero != null) partyConfiguration.Reset(hero);
        return false;
    }

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
}
