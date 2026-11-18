using Common;
using Coop.Naval.CoopSession.Interfaces;
using GameInterface;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;

namespace Coop.Naval.CoopSession.Patches;

[HarmonyPatch(typeof(Campaign))]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class CampaignFigureheadPatches
{
    // The server's list isn't any player's, so an unlock made as a player, like a naval incident's, goes to that player's NavalPlayerData
    [HarmonyPatch(nameof(Campaign.UnlockFigurehead))]
    [HarmonyPrefix]
    private static bool UnlockFigureheadPrefix(Figurehead figurehead)
    {
        if (ModInformation.IsClient) return true;

        var playerHero = Hero.MainHero;
        if (playerHero == null || !playerHero.IsPlayerHero()) return true;
        if (!ContainerProvider.TryResolve<ISessionNavalPlayerDataInterface>(out var sessionNavalPlayerDataInterface)) return true;

        sessionNavalPlayerDataInterface.UnlockFigurehead(playerHero, figurehead);
        return false;
    }
}
