using Common;
using GameInterface.Services.MapEvents.Extensions;
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
            Game.Current?.GameStateManager?.ActiveState != __instance) return;
        // A raid's menu init can run while the scoreboard is leaving; retry the preserved results once it closes.
        if (__instance.IsSimulationActive) return;
        var encounter = PlayerEncounter.Current;
        if (!HasLootToResume(encounter) ||
            Campaign.Current.MapEventManager.MapEvents.Contains(encounter._mapEvent)) return;
        // A captured lord conversation is open; updating now would reopen it every tick.
        if (Campaign.Current.ConversationManager?.IsConversationInProgress == true) return;

        // Custom hideout menus, the siege defeated menu and an empty map have no native menu init callback
        // to resume the loot flow.
        PlayerEncounter.Update();
    }

    // Whether a hideout, naval or siege assault defender encounter must stay open after its map event is destroyed.
    internal static bool HoldsBattleResultEncounter(PlayerEncounter encounter) =>
        encounter?._mapEvent is MapEvent mapEvent &&
        (mapEvent.IsHideoutBattle || mapEvent.IsNavalMapEvent ||
         MapEventInitializationBarrier.IsStagedRaidSimulationResult(encounter) ||
         (mapEvent.IsSiegeAssault && encounter.PlayerSide == BattleSideEnum.Defender)) &&
        MapEventInitializationBarrier.IsBattleResultEncounter(encounter);

    // A held send troops encounter waits on its result menu, because the server commits no loot for it.
    private static bool HasLootToResume(PlayerEncounter encounter) =>
        HoldsBattleResultEncounter(encounter) && !encounter._mapEvent.IsSendTroopsHideout();
}
