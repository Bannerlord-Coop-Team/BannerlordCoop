#if DEBUG
using Common.Commands;
using Common.Messaging;
using Missions.Messages;
using Missions.Battles;
using NavalDLC.Missions.Objects;
using System;
using System.Globalization;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.capture 1d93
// coop.debug.naval.capture status 1d93
// coop.debug.naval.capture 1d93 force
/// <summary>
/// DEBUG: puts this client's main agent at a hull's helm the way a player takes it, so vanilla's 3 s capture timer runs and
/// its end goes to the hull's authority; <c>status</c> reports the gates, timer and request; <c>force</c> sends the request now.
/// </summary>
public sealed class NavalCaptureCoopCommand : ICoopCommand
{
    internal const string Usage = "Usage: coop.debug.naval.capture <shipId-prefix> [force] | status <shipId-prefix>";

    private const string StatusWord = "status";
    private const string ForceWord = "force";

    public string Prefix => "coop.debug.naval";

    public string Name => "capture";

    public string Description => "Moves the main agent to an enemy hull's (or its own orphan's) helm and starts using it, so vanilla's " +
        "capture timer runs; status reports the helm gates, the timer and the request to the hull's authority; force sends the " +
        "request at once, past the local helm gates, so the authority's own refusal (NotVacant, Busy...) answers it.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("target", "The start of the hull's ship id, as coop.debug.naval.inspect lists it, or status.", true),
        new ExpectedArgs("option", "After a ship id: force, to send the request past the local helm gates. After status: the ship id.", false),
    };

    // Starts the capture at, reports on, or forces a request for the one registered hull whose ship id starts with the prefix.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!TryParse(args, out var mode, out var prefix)) return Failed(Usage);

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        var naval = mission?.GetMissionBehavior<CoopNavalBattleBehavior>();
        if (controller == null || naval == null) return Failed("No active coop naval battle mission.");
        if (!mission.IsDeploymentFinished) return Failed("The battle is still deploying; capture once deployment has finished.");

        var main = Agent.Main;
        if (main == null || !main.IsActive()) return Failed("There is no living main agent to capture with.");

        var registry = controller.MissionComponent.ShipRegistry;
        var error = NavalReswapCoopCommand.TryResolveShipId(registry.Ships.Select(ship => ship.ShipId), prefix, out var shipId);
        if (error != null) return Failed(error);
        if (!registry.TryGet(shipId, out var info) || info.Hull is not MissionShip hull) return Failed("The hull is not a naval ship.");

        var gates = HelmGates.Read(naval.Engine, main, hull, IsOwnOrphan(info, controller.Session.OwnControllerId));
        if (mode == CaptureMode.Status) return Status(naval, info, gates);
        if (mode == CaptureMode.Force) return Force(naval, main, info);

        return StartAtHelm(main, hull, info, gates);
    }

    /// <summary>Reads <c>ship</c>, <c>ship force</c> or <c>status ship</c>; anything else is a usage error.</summary>
    internal static bool TryParse(ICoopCommandArgs args, out CaptureMode mode, out string prefix)
    {
        mode = CaptureMode.Helm;
        prefix = null;
        if (args == null || args.Count < 1 || args.Count > 2) return false;

        bool status = IsWord(args[0], StatusWord);
        if (args.Count == 1)
        {
            prefix = args[0];
            return !status;
        }

        mode = status ? CaptureMode.Status : CaptureMode.Force;
        prefix = status ? args[1] : args[0];
        return status ? !IsWord(args[1], ForceWord) : IsWord(args[1], ForceWord);
    }

    // A case-insensitive keyword match.
    private static bool IsWord(string value, string word) => string.Equals(value?.Trim(), word, StringComparison.OrdinalIgnoreCase);

    /// <summary>The captor's own orphan, which only it may take back (the authority's NotEnemy rule); it has no team to be an enemy's.</summary>
    internal static bool IsOwnOrphan(NetworkShipInfo info, string ownControllerId) =>
        info != null && info.IsOrphaned && info.CurrentAuthority == ownControllerId;

    // Publishes what vanilla's elapsed helm timer publishes (CoopShipCapturePatch), so the replicator asks the hull's authority
    // exactly as after a real timer; a request already waiting for that hull is reported, not sent again.
    private static CoopCommandResult Force(CoopNavalBattleBehavior naval, Agent main, NetworkShipInfo info)
    {
        if (naval.Engine.CrewHullOf(main) == null)
            return Failed("NAVAL_CAPTURE ship=" + info.ShipId + " force: the main agent crews no hull to capture with.");

        var replicator = naval.ShipCaptureReplicator;
        float? waiting = replicator.PendingRequestAge(info.ShipId);
        if (waiting.HasValue)
            return Failed(string.Format(CultureInfo.InvariantCulture, "NAVAL_CAPTURE ship={0} force: a request to {1} has waited {2:0.00} s; " +
                "nothing sent", info.ShipId, info.CurrentAuthority, waiting.Value));

        MessageBroker.Instance.Publish(main, new ShipCaptureTimerElapsed(info.Hull, main));
        return new CoopCommandResult(true, "NAVAL_CAPTURE ship=" + info.ShipId + " forced past this copy's helm gates: " +
            ForceOutcome(replicator.PendingRequestAge(info.ShipId).HasValue, info.CurrentAuthority) +
            "; see coop.debug.naval.capture status " + info.ShipId + " and captureRejections in coop.debug.naval.inspect");
    }

    /// <summary>What a forced request led to: a request waiting at the hull's authority, or a decision taken here at once.</summary>
    internal static string ForceOutcome(bool requestWaiting, string authority) =>
        requestWaiting ? "request sent to " + authority : "decided here (this client simulates it, or the captor hull was refused)";

    // Takes the helm the way the player's use key does (Agent.HandleStartUsingAction), after the gates vanilla's prompt reads.
    private static CoopCommandResult StartAtHelm(Agent main, MissionShip hull, NetworkShipInfo info, HelmGates gates)
    {
        var refusal = gates.Refusal();
        if (refusal != null) return Failed("NAVAL_CAPTURE ship=" + info.ShipId + " refused here: " + refusal + "; " + gates);

        var point = hull.ShipControllerMachine.PilotStandingPoint;
        if (main.CurrentlyUsedGameObject != null) main.StopUsingGameObject(isSuccessful: true, Agent.StopUsingGameObjectFlags.None);
        main.TeleportToPosition(point.GetUserFrameForAgent(main).Origin.GetGroundVec3());
        main.HandleStartUsingAction(point, -1);

        return new CoopCommandResult(true, "NAVAL_CAPTURE ship=" + info.ShipId + " authority=" + info.CurrentAuthority +
            " started=" + (hull.ShipControllerMachine.PilotAgent == main) + "; vanilla's timer runs 3 s while the hull stays vacant " +
            "and connected, then the request goes to its authority (coop.debug.naval.capture status " + info.ShipId + ")");
    }

    // The helm gates, vanilla's timer, this client's request and the capture the registry holds for the hull.
    private static CoopCommandResult Status(CoopNavalBattleBehavior naval, NetworkShipInfo info, HelmGates gates)
    {
        var helm = ((MissionShip)info.Hull).ShipControllerMachine;
        float? waited = naval.ShipCaptureReplicator.PendingRequestAge(info.ShipId);
        return new CoopCommandResult(true, string.Format(CultureInfo.InvariantCulture,
            "NAVAL_CAPTURE_STATUS ship={0} authority={1} captor={2} orphan={3} timer={4:0.00} requestWaited={5} {6}",
            info.ShipId, info.CurrentAuthority, info.CaptorControllerId ?? "none", info.IsOrphaned, helm?.CaptureTimer ?? -1f,
            waited.HasValue ? waited.Value.ToString("0.00", CultureInfo.InvariantCulture) + " s" : "none", gates));
    }

    // A failed command result with output.
    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");

    /// <summary>What the command does with its hull: take the helm, report on it, or send the request at once.</summary>
    internal enum CaptureMode
    {
        Helm,
        Status,
        Force,
    }

    /// <summary>What vanilla's helm prompt and capture branch read for the main agent at a hull's helm (ShipControllerMachine.OnTick).</summary>
    private readonly struct HelmGates
    {
        private HelmGates(bool hasHelm, bool isEnemy, bool canBeTakenOver, bool vacantHere, bool connected, bool usableByMain,
            bool disabledForPlayers, bool takenByOther, bool mainIsPilot)
        {
            HasHelm = hasHelm;
            IsEnemy = isEnemy;
            CanBeTakenOver = canBeTakenOver;
            VacantHere = vacantHere;
            Connected = connected;
            UsableByMain = usableByMain;
            DisabledForPlayers = disabledForPlayers;
            TakenByOther = takenByOther;
            MainIsPilot = mainIsPilot;
        }

        private bool HasHelm { get; }
        private bool IsEnemy { get; }
        private bool CanBeTakenOver { get; }
        private bool VacantHere { get; }
        private bool Connected { get; }
        private bool UsableByMain { get; }
        private bool DisabledForPlayers { get; }
        private bool TakenByOther { get; }
        private bool MainIsPilot { get; }

        // Reads every gate from the local hulls; the main agent's own hull is the one its formation crews. This client's own
        // orphan has no team but counts as takeable, as the authority's NotEnemy rule treats it.
        public static HelmGates Read(INavalShipEngine engine, Agent main, MissionShip hull, bool ownOrphan)
        {
            var helm = hull.ShipControllerMachine;
            var point = helm?.PilotStandingPoint;
            var mainHull = engine.CrewHullOf(main) as MissionShip;
            return new HelmGates(
                hasHelm: point != null,
                isEnemy: ownOrphan || (hull.Team != null && main.Team != null && hull.Team.IsEnemyOf(main.Team)),
                canBeTakenOver: hull.CanBeTakenOver,
                vacantHere: helm?.IsAttachedShipVacant() == true,
                connected: mainHull != null && MissionShip.AreShipsConnected(mainHull, hull),
                usableByMain: point != null && point.IsUsableByAgent(main),
                disabledForPlayers: point != null && point.IsDisabledForPlayers,
                takenByOther: helm?.PilotAgent != null && helm.PilotAgent != main,
                mainIsPilot: helm?.PilotAgent == main);
        }

        // The first gate that keeps the main agent off the helm, or null.
        public string Refusal()
        {
            if (!HasHelm) return "the hull has no helm";
            if (!IsEnemy) return "the hull is neither an enemy's nor this client's own orphan";
            if (TakenByOther) return "someone is at the helm";
            if (!UsableByMain) return "the helm is AI-only while its formation has units here";
            if (DisabledForPlayers) return "the helm is disabled until the hull is vacant and connected";
            return null;
        }

        // Every gate on one line.
        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "enemy={0} canBeTakenOver={1} vacantHere={2} connected={3} usableByMain={4} disabledForPlayers={5} helmTaken={6} mainIsPilot={7}",
            IsEnemy, CanBeTakenOver, VacantHere, Connected, UsableByMain, DisabledForPlayers, TakenByOther, MainIsPilot);
    }
}
#endif
