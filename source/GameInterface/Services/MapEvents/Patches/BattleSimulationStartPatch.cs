using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages.Start;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MapState))]
internal class BattleSimulationStartPatch
{
    /// <summary>
    /// [Client] Fires when the player opens the auto-resolve simulation screen. The simulation
    /// itself is disabled on clients (see <see cref="BattleSimulationUpdatePatch"/>); we instead
    /// ask the server to run it authoritatively for this map event.
    /// </summary>
    [HarmonyPatch(nameof(MapState.StartBattleSimulation))]
    [HarmonyPostfix]
    private static void Postfix_StartBattleSimulation()
    {
        if (!ModInformation.IsClient)
            return;

        var simulation = PlayerEncounter.CurrentBattleSimulation;
        if (simulation?.MapEvent == null)
            return;

        // A spectator opens this same screen in response to the server's NetworkOpenBattleSimulation; it must not
        // ask the server to run a second simulation. Only the initiating player requests the authoritative run.
        if (BattleSimulationReplay.IsSpectator)
            return;

        MessageBroker.Instance.Publish(simulation, new BattleSimulationStarted(simulation.MapEvent));
    }
}
