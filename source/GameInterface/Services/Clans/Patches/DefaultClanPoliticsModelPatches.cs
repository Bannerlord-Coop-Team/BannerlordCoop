using Common;
using GameInterface.Configuration;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.Players;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(DefaultClanPoliticsModel))]
internal class DefaultClanPoliticsModelPatches
{
    [HarmonyPatch(nameof(DefaultClanPoliticsModel.CalculateInfluenceChangeInternal))]
    [HarmonyPrefix]
    public static bool CalculateInfluenceChangeInternalPrefix(Clan clan, ref ExplainedNumber influenceChange)
    {
        // Calculate influence change for AI led clans normally
        if (clan.Leader == null || !clan.Leader.IsPlayerHero()) return true;

        ContainerProvider.TryResolve<IPlayerManager>(out var playerManager);

        // Don't calculate influence change for disconnected players based on config
        if (ModInformation.IsServer
            && !ModConfigProvider.ModOptions.GoldFoodInfluenceChangeForDisconnectedPlayers
            && playerManager.IsOwnerOfHeroDisconnected(clan.Leader)) return false;

        // Don't calculate influence change when clan leader is in a settlement based on config
        if (clan.Leader.CurrentSettlement != null
            && !ModConfigProvider.ModOptions.GoldFoodInfluenceChangeInSettlements) return false;

        var clanLeaderMapEvent = clan.Leader.PartyBelongedTo?.MapEvent;

        // Clan leader not in a map event, calculate influence change normally
        if (clanLeaderMapEvent == null) return true;

        // Influence change is disabled in battles, skip this calculation
        if (ModConfigProvider.ModOptions.GoldFoodInfluenceChangeInBattles == GoldFoodChangeMode.Disabled) return false;

        // Use gold fold consumption window to determine if the influence change should be calculated based on config.
        // This way players only have an influence change at most once during a map event when set to OneDayMax.
        if (ModConfigProvider.ModOptions.GoldFoodInfluenceChangeInBattles == GoldFoodChangeMode.OneDayMax
            && !InteractionPatches.IsWithinGoldFoodConsumptionWindow(clanLeaderMapEvent)) return false;

        return true;
    }
}
