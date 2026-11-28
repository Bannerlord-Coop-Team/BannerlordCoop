using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using Serilog;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Why <see cref="NavalSetupGuard"/> ends a naval mission before it starts.</summary>
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
/// Ends a naval mission that can't be set up, a coop naval battle or a naval village raid, instead of letting the deployment
/// controller retry its team setup every tick.
/// </summary>
public class NavalSetupGuard : MissionLogic
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalSetupGuard>();

    internal const string SetupFailedMessage = "Unable to start the naval battle. Returning to the campaign map.";

    private readonly IMessageBroker messageBroker;
    private NavalSetupEndReason? endReason;
    private bool endPending;

    public NavalSetupGuard(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
    }

    /// <summary>Set once vanilla's naval team setup threw; a rerun would spawn a fresh hull per formation each tick (R41a-2 S).</summary>
    public bool SetupFailed { get; private set; }

    /// <summary>
    /// [Game thread] Ends the mission from its next tick and tells the player once; a setup failure is latched even after an
    /// earlier end. True when this call recorded anything.
    /// </summary>
    internal bool End(NavalSetupEndReason reason)
    {
        bool newFailure = reason == NavalSetupEndReason.TeamSetupFailed && !SetupFailed;
        if (newFailure) SetupFailed = true;
        if (endReason != null) return newFailure;

        endReason = reason;
        messageBroker.Publish(this, new SendInformationMessage(MessageFor(reason)));
        // Mission.AfterStart sets the state back to Continuing after EarlyStart, and CheckMissionEnd then never ends an
        // already ended mission, so the end waits for the first tick (R41a).
        endPending = true;
        return true;
    }

    /// <summary>The message the player sees when the mission ends for <paramref name="reason"/>.</summary>
    protected virtual string MessageFor(NavalSetupEndReason reason) => SetupFailedMessage;

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
