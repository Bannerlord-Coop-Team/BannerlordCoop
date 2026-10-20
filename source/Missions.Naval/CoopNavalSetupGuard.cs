using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using Missions.Battles;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Why <see cref="CoopNavalSetupGuard"/> ends a naval mission before it starts.</summary>
public enum NavalSetupEndReason
{
    /// <summary>The own troop reserve was not in place when the naval spawn logic pulled it.</summary>
    MissingReserve,

    /// <summary>The player team has no ship or no player troop for vanilla's initial allocation.</summary>
    PlayerTeamNotAllocatable,

    /// <summary>Vanilla's naval team setup threw.</summary>
    TeamSetupFailed,
}

/// <summary>
/// Ends a coop naval battle that can't be set up: its own reserve missing when the spawn logic pulls it once in
/// <c>EarlyStart</c> (no retry, so no hold like the land spawn handler's), or a team setup the setup patches stopped.
/// </summary>
public class CoopNavalSetupGuard : MissionLogic
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopNavalSetupGuard>();

    internal const string SetupFailedMessage = "Unable to start the naval battle. Returning to the campaign map.";

    private readonly CoopTroopSupplier ownSupplier;
    private readonly BattleSideEnum playerSide;
    private readonly IMessageBroker messageBroker;
    private NavalSetupEndReason? endReason;
    private bool endPending;

    public CoopNavalSetupGuard(CoopTroopSupplier ownSupplier, BattleSideEnum playerSide, IMessageBroker messageBroker)
    {
        this.ownSupplier = ownSupplier;
        this.playerSide = playerSide;
        this.messageBroker = messageBroker;
    }

    public bool ReservePresent { get; private set; }

    /// <summary>Set once vanilla's naval team setup threw; the deployment controller must not run it again.</summary>
    public bool SetupFailed { get; private set; }

    /// <summary>
    /// [Game thread] Ends <paramref name="mission"/> through its guard for <paramref name="reason"/>; false once any earlier
    /// reason ends it, except a first setup failure, which is still recorded. With no guard it logs what the caller does.
    /// </summary>
    internal static bool TryEnd(Mission mission, NavalSetupEndReason reason, string outcome)
    {
        var guard = mission?.GetMissionBehavior<CoopNavalSetupGuard>();
        if (guard == null)
        {
            Logger.Error("[NavalBattle] No setup guard ends the naval mission ({Reason}): {Outcome}", reason, outcome);
            return false;
        }

        return reason == NavalSetupEndReason.TeamSetupFailed ? guard.FailSetup() : guard.RequestEnd(reason);
    }

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

        RequestEnd(NavalSetupEndReason.MissingReserve);
    }

    /// <summary>
    /// [Game thread] Ends the mission from its next tick and tells the player why it can't start. True only for the call that
    /// asked first.
    /// </summary>
    internal bool RequestEnd(NavalSetupEndReason reason)
    {
        if (endReason != null) return false;

        endReason = reason;
        messageBroker.Publish(this, new SendInformationMessage(MessageFor(reason)));
        // Mission.AfterStart sets the state back to Continuing after EarlyStart, and CheckMissionEnd then never ends an
        // already ended mission, so the end waits for the first tick (R41a).
        endPending = true;
        return true;
    }

    /// <summary>
    /// [Game thread] Records that the naval team setup threw and ends the mission; a retry would deploy every side again
    /// and spawn a fresh hull per formation each tick (R41a-2 S). True only for the first failure.
    /// </summary>
    internal bool FailSetup()
    {
        if (SetupFailed) return false;

        SetupFailed = true;
        RequestEnd(NavalSetupEndReason.TeamSetupFailed);
        return true;
    }

    // A missing reserve has its own player message; the other reasons are no fault of the player's party.
    private static string MessageFor(NavalSetupEndReason reason) =>
        reason == NavalSetupEndReason.MissingReserve ? CoopBattleMissionSpawnHandler.InvalidPlayerReserveMessage : SetupFailedMessage;

    // Ends a mission that can't be set up; runs before any behaviour's mission tick of the frame.
    public override void OnPreDisplayMissionTick(float dt)
    {
        base.OnPreDisplayMissionTick(dt);
        if (!endPending) return;

        endPending = false;
        Logger.Information("[NavalBattle] Ending the naval mission before it started: {Reason}", endReason);
        Mission.EndMission();
    }
}
