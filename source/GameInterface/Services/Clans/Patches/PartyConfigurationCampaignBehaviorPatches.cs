using GameInterface.Services.Clans.Data;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// v1.5 resets a hero's party commands when they leave a companion role, become the player character or
/// leave the player clan. The behavior runs on the server only, and its reset nulls a field that is not
/// synced, so each reset goes through <see cref="PartyConfigurationFlags.Reset"/> instead. Every co-op
/// player's clan counts as the player clan.
/// </summary>
[HarmonyPatch(typeof(PartyConfigurationCampaignBehavior))]
internal class PartyConfigurationCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PartyConfigurationCampaignBehavior.OnCompanionRemoved))]
    [HarmonyPrefix]
    private static bool OnCompanionRemovedPrefix(Hero hero)
    {
        if (hero != null) PartyConfigurationFlags.Reset(hero);
        return false;
    }

    [HarmonyPatch(nameof(PartyConfigurationCampaignBehavior.OnBeforePlayerCharacterChanged))]
    [HarmonyPrefix]
    private static bool OnBeforePlayerCharacterChangedPrefix(Hero hero)
    {
        if (hero != null) PartyConfigurationFlags.Reset(hero);
        return false;
    }

    // The event passes the clan the hero left.
    [HarmonyPatch(nameof(PartyConfigurationCampaignBehavior.OnHeroChangedClan))]
    [HarmonyPrefix]
    private static bool OnHeroChangedClanPrefix(Hero hero, Clan clan)
    {
        if (clan != null && (clan == Clan.PlayerClan || clan.IsPlayerClan()))
            PartyConfigurationFlags.Reset(hero);
        return false;
    }
}
