using Common;
using GameInterface.Configuration;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Players;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(Town))]
internal class TownTradeTaxAccumulatedCapPatch
{
    // Artificial cap. Players always receive 1/5 of the trade tax acculumated normally so there is no available vanilla threshold.
    // At 25000, a player would only receive 5000 when coming back online after being gone a while instead of a huge stack.
    // This also applies to recently conquered settlements from another disconnected player.
    private static readonly int TradeTaxAccumulatedCap = 25000;

    [HarmonyPatch(nameof(Town.TradeTaxAccumulated), MethodType.Setter)]
    [HarmonyPostfix]
    public static void TradeTaxAccumulatedSetterPostfix(Town __instance)
    {
        if (__instance.TradeTaxAccumulated <= TradeTaxAccumulatedCap) return;

        if (ModInformation.IsClient) return;

        // Cap trade tax acculumulated for disconnected players based on config
        // When capped at TradeTaxAccumulatedCap (25000), rejoining players and recent conquerers won't see a spike in town trade tariffs
        if (ModConfigProvider.ModOptions.GoldFoodInfluenceChangeForDisconnectedPlayers) return;

        if (ContainerProvider.TryResolve<IPlayerManager>(out IPlayerManager playerManager) == false) return;

        var owner = __instance.Settlement?.Owner;
        if (owner == null || !owner.IsPlayerHero()) return;

        if (playerManager.IsOwnerOfHeroDisconnected(owner))
        {
            __instance.TradeTaxAccumulated = TradeTaxAccumulatedCap;
        }
    }
}
