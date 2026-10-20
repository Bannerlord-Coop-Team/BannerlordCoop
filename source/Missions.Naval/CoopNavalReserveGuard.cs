using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using HarmonyLib;
using Missions.Battles;
using NavalDLC;
using NavalDLC.Missions.MissionLogics;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// Ends a coop naval battle whose own troop reserve is not in place when the naval spawn logic pulls its troops.
/// <c>DefaultNavalMissionAgentSpawnLogic</c> pulls every troop once, in <c>EarlyStart</c>, with no later retry,
/// so there is no hold like the land spawn handler's: the battle fails clearly instead.
/// </summary>
public class CoopNavalReserveGuard : MissionLogic
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopNavalReserveGuard>();

    private readonly CoopTroopSupplier ownSupplier;
    private readonly BattleSideEnum playerSide;
    private readonly IMessageBroker messageBroker;
    private bool endPending;

    public CoopNavalReserveGuard(CoopTroopSupplier ownSupplier, BattleSideEnum playerSide, IMessageBroker messageBroker)
    {
        this.ownSupplier = ownSupplier;
        this.playerSide = playerSide;
        this.messageBroker = messageBroker;
    }

    public bool ReservePresent { get; private set; }

    // Checks the own reserve as the spawn logic pulls it; a missing one ends the mission at its first tick.
    public override void EarlyStart()
    {
        base.EarlyStart();
        ReservePresent = ownSupplier.IsPopulated && ownSupplier.GetRemainingForParty(ownSupplier.PlayerPartyId) > 0;
        if (ReservePresent)
        {
            Logger.Information("[NavalBattle] Own reserve present at naval spawn: side={Side} party={PartyId} troops={Troops}",
                playerSide, ownSupplier.PlayerPartyId, ownSupplier.GetRemainingForParty(ownSupplier.PlayerPartyId));
            return;
        }

        Logger.Error("[NavalBattle] Own reserve missing at naval spawn: side={Side} populated={Populated} party={PartyId}; ending the mission",
            playerSide, ownSupplier.IsPopulated, ownSupplier.PlayerPartyId);

        // An unpopulated supplier reports troops remaining forever, which would spin the vanilla initial pull.
        if (!ownSupplier.IsPopulated)
            ownSupplier.SetReserve(Array.Empty<PartyReserve>(), 0, 0, 0);

        messageBroker.Publish(this, new SendInformationMessage(CoopBattleMissionSpawnHandler.InvalidPlayerReserveMessage));
        // Mission.AfterStart sets the state back to Continuing after EarlyStart, and CheckMissionEnd then never ends an
        // already ended mission, so the end waits for the first tick (R41a).
        endPending = true;
    }

    // Ends a mission whose reserve was missing; runs before any behaviour's mission tick of the frame.
    public override void OnPreDisplayMissionTick(float dt)
    {
        base.OnPreDisplayMissionTick(dt);
        if (!endPending) return;

        endPending = false;
        Logger.Information("[NavalBattle] Ending the naval mission with no own reserve");
        Mission.EndMission();
    }
}

// With no own troops vanilla assigns the player's team no ship, then puts the player troop on that missing hull every tick
// (NRE in NavalAgentsLogic.AddReservedTroopToShip, R41a); there is nothing to deploy, so the allocation is skipped.
[HarmonyPatch(typeof(NavalTeamSideSpawnContext), nameof(NavalTeamSideSpawnContext.AllocateAndDeployInitialTroopsOfPlayerTeam))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CoopNavalShiplessPlayerTeamPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopNavalShiplessPlayerTeamPatch>();

    // Runs the player team's initial allocation only when its first formation has a ship.
    [HarmonyPrefix]
    private static bool Prefix(NavalTeamSideSpawnContext __instance)
    {
        if (AllocatesPlayerTeam(__instance._shipsLogic.GetShipAssignment(TeamSideEnum.PlayerTeam, FormationClass.Infantry)))
            return true;

        Logger.Warning("[NavalBattle] The player's team has no assigned ship; skipping its initial troop allocation");
        return false;
    }

    // Whether vanilla's player-team allocation has a ship to put the player troop on.
    internal static bool AllocatesPlayerTeam(ShipAssignment assignment) => assignment?.HasMissionShip == true;
}
