using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEventComponents.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.Players;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEventComponents.Patches;

[HarmonyPatch(typeof(BlockadeBattleMapEvent))]
internal class PlayerBlockadePatches
{
    // Replace MobileParty.IsMainParty usage so an online player is asked instead of the blockade lifting on its own
    [HarmonyPatch(nameof(BlockadeBattleMapEvent.OnInitialize))]
    [HarmonyPrefix]
    private static bool OnInitializePrefix(BlockadeBattleMapEvent __instance)
    {
        if (ModInformation.IsClient || !IsLedByOnlinePlayer(__instance)) return true;

        __instance._isInitializationNotFinished = false;
        MessageBroker.Instance.Publish(__instance, new PlayerBlockadeAttacked(__instance.MapEvent));
        return false;
    }

    // Only the server decides whether a blockade lifts without a battle
    [HarmonyPatch(nameof(BlockadeBattleMapEvent.OnPartyAdded))]
    [HarmonyPrefix]
    private static bool OnPartyAddedPrefix(BlockadeBattleMapEvent __instance) =>
        ModInformation.IsServer && !IsLedByOnlinePlayer(__instance);

    private static bool IsLedByOnlinePlayer(BlockadeBattleMapEvent blockadeBattle)
    {
        var leaderParty = blockadeBattle.MapEvent.DefenderSide.LeaderParty.MobileParty;
        if (leaderParty?.IsPlayerParty() != true) return false;

        // An offline player can't answer, so their blockade is left to vanilla
        return ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) &&
            !playerManager.IsOwnerOfHeroDisconnected(leaderParty.LeaderHero);
    }
}

[HarmonyPatch(typeof(EncounterGameMenuBehavior))]
internal class PlayerBlockadeMenuPatches
{
    // Vanilla only sets up the encounter fields, so the encounter adopts the server's blockade battle like PromptSiegeAssault
    [HarmonyPatch(nameof(EncounterGameMenuBehavior.defend_blockade_on_consequence))]
    [HarmonyPrefix]
    private static bool DefendBlockadePrefix()
    {
        if (MobileParty.MainParty.MapEvent == null) return true;

        using (new AllowedThread())
        {
            PlayerEncounter.Init();
            PlayerEncounter.Current.SetIsBlockadeAttack(true);
        }

        GameMenu.SwitchToMenu("encounter");
        return false;
    }

    // Lifting deactivates the blockade, ends the battle and moves the attackers, which the server does
    [HarmonyPatch(nameof(EncounterGameMenuBehavior.lift_players_blockade))]
    [HarmonyPrefix]
    private static bool LiftPlayersBlockadePrefix()
    {
        var mapEvent = MobileParty.MainParty.MapEvent;
        if (mapEvent == null) return true;

        MessageBroker.Instance.Publish(mapEvent, new LiftPlayerBlockadeAttempted(mapEvent));
        GameMenu.SwitchToMenu("menu_siege_strategies");
        return false;
    }
}
