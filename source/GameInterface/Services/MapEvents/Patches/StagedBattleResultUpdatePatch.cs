using Common;
using GameInterface.Services.MapEvents.Initialization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Resumes staged battle results after the server removes the event and its presentation closes.
/// </summary>
[HarmonyPatch(typeof(MapState), "OnMapModeTick")]
internal static class StagedBattleResultUpdatePatch
{
    private static void Postfix(MapState __instance)
    {
        if (ModInformation.IsServer || Mission.Current != null || MissionState.Current != null ||
            Game.Current?.GameStateManager?.ActiveState != __instance || __instance.IsSimulationActive) return;
        var encounter = PlayerEncounter.Current;
        if (!HasStagedResults(encounter) ||
            Campaign.Current.MapEventManager.MapEvents.Contains(encounter._mapEvent)) return;
        // A captured lord conversation is open; updating now would reopen it every tick.
        if (Campaign.Current.ConversationManager?.IsConversationInProgress == true) return;

        // Custom hideout menus, the siege defeated menu and an empty map have no native menu init
        // callback to resume the loot flow. A raid's menu initialization can also run while the
        // scoreboard is leaving, so retry the preserved results after PlayerEncounterPatches stops
        // deferring them for that screen.
        PlayerEncounter.Update();
    }

    internal static bool HasStagedResults(PlayerEncounter encounter) =>
        encounter?._mapEvent is MapEvent mapEvent &&
        (mapEvent.IsHideoutBattle ||
         (mapEvent.IsRaidHostileAction() && encounter.BattleSimulation != null) ||
         (mapEvent.IsSiegeAssault && encounter.PlayerSide == BattleSideEnum.Defender)) &&
        MapEventInitializationBarrier.IsBattleResultEncounter(encounter);
}
