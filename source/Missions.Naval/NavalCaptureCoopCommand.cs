#if DEBUG
using Common.Commands;
using Missions.Battles;
using NavalDLC.Missions.Objects;
using System;
using System.Globalization;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.capture 1d93
// coop.debug.naval.capture status 1d93
/// <summary>
/// DEBUG: puts this client's main agent at an enemy hull's helm the way a player takes it, so vanilla's 3 s capture timer
/// runs and its end goes to the hull's authority; <c>status</c> reports the helm gates, the timer and the request.
/// </summary>
public sealed class NavalCaptureCoopCommand : ICoopCommand
{
    internal const string Usage = "Usage: coop.debug.naval.capture <shipId-prefix> | status <shipId-prefix>";

    public string Prefix => "coop.debug.naval";

    public string Name => "capture";

    public string Description => "Moves the main agent to an enemy hull's helm and starts using it, so vanilla's capture timer runs; " +
        "status reports the helm gates, the timer and the request to the hull's authority.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("target", "The start of the hull's ship id, as coop.debug.naval.inspect lists it, or status followed by it.", true),
    };

    // Starts the capture at, or reports on, the one registered hull whose ship id starts with the prefix.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args == null || args.Count < 1 || args.Count > 2) return Failed(Usage);
        bool status = string.Equals(args[0].Trim(), "status", StringComparison.OrdinalIgnoreCase);
        if (status != (args.Count == 2)) return Failed(Usage);

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        var naval = mission?.GetMissionBehavior<CoopNavalBattleBehavior>();
        if (controller == null || naval == null) return Failed("No active coop naval battle mission.");
        if (!mission.IsDeploymentFinished) return Failed("The battle is still deploying; capture once deployment has finished.");

        var main = Agent.Main;
        if (main == null || !main.IsActive()) return Failed("There is no living main agent to capture with.");

        var registry = controller.MissionComponent.ShipRegistry;
        var error = NavalReswapCoopCommand.TryResolveShipId(registry.Ships.Select(ship => ship.ShipId), args[args.Count - 1], out var shipId);
        if (error != null) return Failed(error);
        if (!registry.TryGet(shipId, out var info) || info.Hull is not MissionShip hull) return Failed("The hull is not a naval ship.");

        var gates = HelmGates.Read(naval.Engine, main, hull);
        if (status) return Status(naval, info, gates);

        return StartAtHelm(main, hull, info, gates);
    }

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

        // Reads every gate from the local hulls; the main agent's own hull is the one its formation crews.
        public static HelmGates Read(INavalShipEngine engine, Agent main, MissionShip hull)
        {
            var helm = hull.ShipControllerMachine;
            var point = helm?.PilotStandingPoint;
            var mainHull = engine.CrewHullOf(main) as MissionShip;
            return new HelmGates(
                hasHelm: point != null,
                isEnemy: hull.Team != null && main.Team != null && hull.Team.IsEnemyOf(main.Team),
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
            if (!IsEnemy) return "the hull is not an enemy's";
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
