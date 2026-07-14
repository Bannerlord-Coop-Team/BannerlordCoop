using Common;
using Common.Logging;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// [Client] Gates the "send your troops to attack" auto-resolve behind the server. Hooks the menu option's
/// consequence (the click) and blocks on the server's accept before letting the scoreboard open — the consequence
/// frozen mid-call keeps the encounter menu in place during the round trip (the same shape as the attack's blocking
/// StartBattleInternal). The server accepts only if no live mission already owns the event
/// (<see cref="ServerBattleModeArbiter"/>); on accept this client becomes the pacer and the native consequence opens
/// the scoreboard, on reject nothing happens and the menu stays open. Other clients open as spectators via the
/// server's open broadcast.
/// </summary>
[HarmonyPatch]
internal class BattleSimulationStartPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleSimulationStartPatch>();

    private const string ConsequenceName = "game_menu_encounter_order_attack_on_consequence";

    static IEnumerable<MethodBase> TargetMethods()
    {
        var method = AccessTools.Method(typeof(EncounterGameMenuBehavior), ConsequenceName);
        if (method == null)
            Logger.Error("Could not find {Method} to patch; the auto-resolve will not be gated by the server", ConsequenceName);
        else
            yield return method;
    }

    [HarmonyPrefix]
    private static bool Prefix()
    {
        // Server / single-player: run the consequence normally.
        if (ModInformation.IsServer)
            return true;

        Logger.Information(
            "[PvPBattleEncounterTrace] Battle encounter option clicked: order attack; party={PartyId} mapEvent={MapEventId} menu={Menu} encounter={Encounter}",
            BattleTrace.DescribePartyForTrace(MobileParty.MainParty?.Party),
            BattleTrace.DescribeMapEventForTrace(BattleTrace.GetCurrentMapEventForTrace()),
            Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>",
            PlayerEncounter.Current != null);

        var coordinator = BattleStartCoordinator.Instance;
        if (coordinator == null)
            return true; // not wired (shouldn't happen in a live session) — fall back to native behavior

        var mapEvent = BattleTrace.GetPlayerEncounterBattleForTrace() ?? MobileParty.MainParty?.MapEvent;
        if (mapEvent == null)
            return true;

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return true;
        if (!objectManager.TryGetId(mapEvent, out var mapEventId))
            return true;

        // Block until the server accepts/rejects. The menu stays open during the wait (the consequence is frozen
        // mid-call). On reject, skip the native consequence so nothing opens and the menu stays.
        if (!coordinator.RequestBlocking(BattleStartMode.Simulation, mapEventId, null))
            return false;

        // Accepted: become the pacer before the scoreboard opens, then let the native consequence open it.
        BattleSimulationReplay.Begin(mapEventId, spectator: false);
        return true;
    }

}
