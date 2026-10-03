#if DEBUG
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Players;
using GameInterface.Services.PlayerCaptivityService.Messages;
using LiteNetLib;
using Missions.Messages;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Serilog;

namespace Missions.Battles;

public interface INavalLabCoordinator
{
    object Create(Guid operationId, string firstController, string secondController, NavalLabMode mode);
    object Execute(Guid operationId, string kind, int ship, float rudder, bool row);
    object Inspect();
    object SailStatus();
    object HelmStatus();
    object ControlStatus();
    object StartDrift(Guid operationId, int seconds);
    object InspectDrift();
}

public sealed partial class NavalLabCoordinator : INavalLabCoordinator, IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalLabCoordinator>();
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly INavalLabSessionStore store;
    private readonly IPlayerManager players;
    private readonly IBattleHostRegistry hosts;
    private readonly IControllerIdProvider own;
    private readonly INavalMissionAdapterLoader loader;
    private readonly Func<INavalLabController> controllerFactory;
    private readonly HashSet<string> ready = new HashSet<string>();
    private readonly HashSet<string> sceneReady = new HashSet<string>();
    private INavalMissionAdapter adapter;
    private INavalLabController controller;
    private Guid startOperation;
    private string failure;
    private object lastNativeStartup;
    private Guid holdOperation;
    private Guid stopOperation;

    public NavalLabCoordinator(IMessageBroker broker, INetwork network, INavalLabSessionStore store,
        IPlayerManager players, IBattleHostRegistry hosts, IControllerIdProvider own,
        INavalMissionAdapterLoader loader, Func<INavalLabController> controllerFactory, INavalLabNativeState nativeState)
    {
        this.broker = broker;
        this.network = network;
        this.store = store;
        this.players = players;
        this.hosts = hosts;
        this.own = own;
        this.loader = loader;
        this.controllerFactory = controllerFactory;
        this.nativeState = nativeState;
        broker.Subscribe<NetworkNavalLabShipSample>(ReceiveShipSample);
        broker.Subscribe<NetworkNavalLabStations>(ReceiveStations);
        broker.Subscribe<NetworkNavalLabHelmOccupancy>(ReceiveHelmOccupancy);
        broker.Subscribe<NetworkNavalLabStart>(ReceiveStart);
        broker.Subscribe<NetworkNavalLabAction>(ReceiveAction);
        broker.Subscribe<NetworkNavalLabReceipt>(ReceiveReceipt);
        broker.Subscribe<NetworkNavalLabFault>(ReceiveFault);
        broker.Subscribe<NetworkNavalLabRopeFinal>(ReceiveFinalRopes);
        broker.Subscribe<CampaignTick>(OnCampaignTick);
    }

    public object Create(Guid operationId, string firstController, string secondController, NavalLabMode mode)
    {
        return CreateFixture(operationId, new[] { firstController, secondController }, mode);
    }

    private object CreateFixture(Guid operationId, string[] controllers, NavalLabMode mode)
    {
        if (ModInformation.IsClient) throw new InvalidOperationException("Run create on the authoritative server.");
        if (!ModInformation.IsNavalLab) throw new InvalidOperationException("Restart with the explicit scoped naval lab opt-in.");
        var peers = new List<NetPeer>();
        foreach (var id in controllers)
        {
            if (string.IsNullOrWhiteSpace(id) || !players.TryGetPeer(id, out var peer) || peers.Contains(peer))
                throw new InvalidOperationException("Distinct connected client identities are required.");
            peers.Add(peer);
        }
        if (!Enum.IsDefined(typeof(NavalLabMode), mode)) throw new ArgumentException("Unknown lab mode.", nameof(mode));
        var contents = "create:" + string.Join(":", controllers) + ":" + mode;
        if (!store.BeginOperation(operationId, contents)) return store.InspectOperation(operationId);
        if (store.Current != null) throw new InvalidOperationException("Restart the isolated run before creating another physics probe.");
        var incarnation = Guid.NewGuid();
        var manifest = new NavalLabManifest("naval-lab:" + incarnation.ToString("N"), incarnation,
            controllers,
            Enumerable.Range(0, controllers.Length * NavalLabManifest.CrewPerShip).Select(_ => Guid.NewGuid()).ToArray(),
            controllers.Select(_ => Guid.NewGuid()).ToArray(), mode);
        store.Install(manifest);
        nativeState.Initialize(manifest);
        store.BeginOperation(incarnation, "activation");
        startOperation = operationId;
        var message = new NetworkNavalLabStart(manifest.InstanceId, incarnation, manifest.Controllers, manifest.Combatants, manifest.Ships, manifest.Mode);
        foreach (var peer in peers) network.Send(peer, message);
        return new { manifest, operation = store.InspectOperation(operationId) };
    }

    public object Execute(Guid operationId, string kind, int ship, float rudder, bool row) =>
        ExecuteCore(operationId, kind, ship, rudder, row);

    private object ExecuteCore(Guid operationId, string kind, int ship, float rudder, bool row, int ropeTargetStation = -1)
    {
        if (ModInformation.IsClient) throw new InvalidOperationException("Run actions on the authoritative server.");
        if (store.Current == null) throw new InvalidOperationException("No lab exists.");
        bool deployment = kind == "complete-deployment";
        bool rope = kind == "rope-throw" || kind == "rope-miss" || kind == "rope-cut" || kind == "rope-plank-force";
        if (rope && (store.Current.Mode != NavalLabMode.TwoClientNative || row || rudder < 0 || rudder >= 32
            || rudder != Math.Truncate(rudder) || (kind == "rope-throw" ? ropeTargetStation < 0 || ropeTargetStation >= NetworkNavalLabRopeState.MaxTargetStations : ropeTargetStation != -1)))
            throw new ArgumentException("Ropes require normal two-client-native and valid station indexes.");
        bool pulse = kind == "native-axes-pulse" || kind == "native-axes-backward" || kind == "native-axes-neutral" || kind == "native-row-stop";
        if (pulse && kind != "native-axes-pulse" && (row || (kind != "native-axes-backward" && rudder != 0)))
            throw new ArgumentException("Backward requires row=false; neutral/row-stop also require rudder=0.");
        bool nativeHelm = kind == "native-take-helm" || kind == "native-release-helm";
        if (nativeHelm && (rudder != 0 || row))
            throw new ArgumentException("Native helm test actions require rudder=0 and row=false.");
        bool sail = kind == "sail-full" || kind == "sail-raised" || kind == "sail-square-raised";
        if (sail && (rudder != 0 || row))
            throw new ArgumentException("Sail test actions require rudder=0 and row=false.");
        bool deckWalk = store.Current.Mode == NavalLabMode.TwoClientNative && (kind == "walk" || kind == "turn");
        if (kind != "stop" && !deployment && !sail && !nativeHelm && !pulse && !rope && !deckWalk)
            throw new InvalidOperationException("Native controls use keyboard input; only the documented test actions and stop are commands.");
        if (deployment && (ship != 0 || rudder != 0 || row)) throw new ArgumentException("Deployment requires ship=0, rudder=0, row=false.");
        if (deckWalk && row) throw new ArgumentException("Deck locomotion requires row=false.");
        if (ship < 0 || ship >= store.Current.Ships.Length || float.IsNaN(rudder) || float.IsInfinity(rudder) || (!rope && Math.Abs(rudder) > 1))
            throw new ArgumentException("Invalid ship or control value.");
        string contents = kind + ":" + ship + ":" + rudder.ToString("R", CultureInfo.InvariantCulture) + ":" + row;
        if (rope) contents += ":" + ropeTargetStation;
        if (kind != "stop" && store.TryInspectOperation(operationId, contents, out var existingReceipt)) return existingReceipt;
        if ((nativeHelm || pulse || rope || deckWalk) && !nativeReleaseSent) throw new InvalidOperationException("Native stations are not ready.");
        if (!hosts.TryGet(store.Current.InstanceId, out var host) && kind != "stop")
            throw new InvalidOperationException("The fixture has no elected mission-ready host.");
        if (kind != "stop" && (host.Epoch != 1 || ready.Count != store.Current.Controllers.Length || failure != null
            || store.Current.Controllers.Any(id => !players.TryGetPeer(id, out _)
                || (id != host.HostControllerId && !host.SuccessorControllerIds.Contains(id)))))
            throw new InvalidOperationException("All original owners and epoch-one readiness are required.");
        if (kind == "stop")
        {
            if (stopOperation != Guid.Empty) return store.InspectOperation(stopOperation);
            store.BeginEmergencyOperation(operationId, "stop");
            stopOperation = operationId;
        }
        else if (!store.BeginOperation(operationId, contents)) return store.InspectOperation(operationId);
        var action = new NetworkNavalLabAction(store.Current.IncarnationId, operationId, host?.Epoch ?? 0, kind, ship, rudder, row,
            (nativeHelm || rope) ? DateTime.UtcNow.AddSeconds(2).Ticks : (sail || pulse || deckWalk) ? DateTime.UtcNow.AddSeconds(1).Ticks : 0,
            ropeTargetStation);
        var targets = kind == "stop" || deployment ? store.Current.Controllers : new[] { store.Current.Controllers[ship] };
        foreach (var target in targets)
            if (players.TryGetPeer(target, out var peer)) network.Send(peer, action);
        return store.InspectOperation(operationId);
    }

    public object StartDrift(Guid operationId, int seconds)
    {
        if (!GameThread.Instance.IsGameThread) throw new InvalidOperationException("Drift recording requires the game thread.");
        if (!HasFixture || adapter is not INavalDriftAdapter drift) throw new InvalidOperationException("No native client drift adapter.");
        return drift.StartDrift(operationId, seconds);
    }

    public object InspectDrift()
    {
        if (!GameThread.Instance.IsGameThread) throw new InvalidOperationException("Drift inspection requires the game thread.");
        return (adapter as INavalDriftAdapter)?.InspectDrift() ?? new { unavailable = "no_native_drift_adapter" };
    }

    public object HelmStatus()
    {
        if (!GameThread.Instance.IsGameThread) throw new InvalidOperationException("Read helm status on the game thread.");
        return new
        {
            incarnation = store.Current?.IncarnationId, mode = store.Current?.Mode.ToString(),
            status = HasFixture ? (adapter as INavalNativeMissionAdapter)?.InspectHelmStatus() : null,
            terminalState = controller?.TerminalStatus(),
            unavailable = !HasFixture ? "no_fixture" : adapter == null ? "no_local_native_view" : null
        };
    }

    public object ControlStatus()
    {
        if (!GameThread.Instance.IsGameThread) throw new InvalidOperationException("Read control status on the game thread.");
        return new
        {
            incarnation = store.Current?.IncarnationId, mode = store.Current?.Mode.ToString(),
            host = store.Current != null && hosts.TryGet(store.Current.InstanceId, out var assignment)
                ? new { assignment.HostControllerId, assignment.Epoch } : null,
            status = HasFixture ? (adapter as INavalNativeMissionAdapter)?.InspectControlStatus() : null,
            transport = HasFixture ? (controller as INavalNativeController)?.NativeControlStatus() : null,
            expectedBothBodiesActive = store.Current?.AllPhysicsProbe == true,
            serverShipSequences = shipSampleSequences, serverShipLastReject = shipSampleRejections,
            presentation = HasFixture ? (adapter as INavalPresentationAdapter)?.InspectPresentationStatus() : null,
            unavailable = !HasFixture ? "no_fixture" : adapter == null ? "no_local_native_view" : null
        };
    }

    public object SailStatus()
    {
        if (!GameThread.Instance.IsGameThread) throw new InvalidOperationException("Read sail status on the game thread.");
        return new
        {
            incarnation = store.Current?.IncarnationId, mode = store.Current?.Mode.ToString(),
            host = store.Current != null && hosts.TryGet(store.Current.InstanceId, out var assignment)
                ? new { assignment.HostControllerId, assignment.Epoch } : null,
            status = HasFixture ? (adapter as INavalNativeMissionAdapter)?.InspectSailStatus() : null,
            unavailable = !HasFixture ? "no_fixture" : adapter == null ? "no_local_native_view" : null
        };
    }

    public object Inspect() => new
    {
        manifest = store.Current,
        ready = ready.ToArray(),
        sceneReady = sceneReady.ToArray(),
        failure,
        nativeStartup = adapter?.StartupDiagnostics ?? lastNativeStartup,
        native = adapter?.Inspect(),
        adapterMvid = adapter?.GetType().Assembly.ManifestModule.ModuleVersionId,
        host = store.Current != null && hosts.TryGet(store.Current.InstanceId, out var host) ? host : null,
        campaignWriteBlocker = store.CampaignWriteBlocker,
        nativeControls = HasFixture ? new { ready = nativeState.Ready, stationsCommitted, nativeReleaseSent, stations = nativeState.Stations } : null,
        recoverySupported = false
    };

    private void ReceiveStart(MessagePayload<NetworkNavalLabStart> payload)
    {
        if (ModInformation.IsServer || !ModInformation.IsNavalLab) return;
        GameThread.RunSafe(() =>
        {
            var message = payload.What;
            try
            {
                var manifest = new NavalLabManifest(message.InstanceId, message.IncarnationId,
                    message.Controllers, message.Combatants, message.Ships, message.Mode);
                if (!manifest.Controllers.Contains(own.ControllerId)) return;
                if (!store.BeginOperation(message.IncarnationId, "activation")) return;
                adapter = loader.Load();
                adapter.Preflight();
                store.Install(manifest);
                nativeState.Initialize(manifest);
                controller = controllerFactory();
                Logger.Information("[NavalLabStartup] {Incarnation} open begin missionsMvid={MissionsMvid} adapterMvid={AdapterMvid}",
                    manifest.IncarnationId, typeof(NavalLabCoordinator).Assembly.ManifestModule.ModuleVersionId,
                    adapter.GetType().Assembly.ManifestModule.ModuleVersionId);
                controller.Start(manifest, adapter, () =>
                {
                    lastNativeStartup = adapter?.StartupDiagnostics;
                    failure = failure ?? adapter?.Blocker;
                    controller = null;
                    loader.Dispose();
                    adapter = null;
                });
            }
            catch (Exception exception)
            {
                failure = exception.ToString();
                Logger.Error(exception, "[NavalLabStartup] {Incarnation} open failed before rollback", message.IncarnationId);
                lastNativeStartup = adapter?.StartupDiagnostics;
                try { controller?.AbortStart(); }
                finally
                {
                    controller = null;
                    loader.Dispose();
                    adapter = null;
                    network.SendAll(new NetworkNavalLabReceipt(message.IncarnationId, message.IncarnationId, "failed:" + failure));
                }
            }
        }, context: nameof(ReceiveStart));
    }

    private void ReceiveAction(MessagePayload<NetworkNavalLabAction> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() =>
        {
            var action = payload.What;
            if (store.Current?.IncarnationId != action.IncarnationId || controller == null) return;
            string contents = action.Kind + ":" + action.Ship + ":" + action.Rudder.ToString("R", CultureInfo.InvariantCulture) + ":" + action.Row;
            if (action.Kind.StartsWith("rope-", StringComparison.Ordinal)) contents += ":" + action.RopeTargetStation;
            bool first = action.Kind == "hold" || action.Kind == "stop"
                ? store.BeginEmergencyOperation(action.OperationId, action.Kind)
                : store.BeginOperation(action.OperationId, contents);
            if (!first) return;
            string status = controller.Apply(action);
            store.RecordReceipt(action.OperationId, own.ControllerId, status);
            network.SendAll(new NetworkNavalLabReceipt(action.IncarnationId, action.OperationId, status));
            if (action.Kind == "complete-deployment" && status == "deployed")
            {
                try { network.SendAll(((INavalNativeController)controller).CreateStations()); }
                catch (Exception exception) { network.SendAll(new NetworkNavalLabFault(action.IncarnationId, exception.ToString())); }
            }
        }, context: nameof(ReceiveAction));
    }

    private void ReceiveReceipt(MessagePayload<NetworkNavalLabReceipt> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            var receipt = payload.What;
            if (store.Current?.IncarnationId != receipt.IncarnationId || payload.Who is not NetPeer peer
                || !players.TryGetPlayer(peer, out var player)
                || !store.IsParticipant(store.Current.InstanceId, player.ControllerId)) return;
            if (receipt.OperationId == store.Current.IncarnationId)
            {
                if (receipt.Status == "scene_ready") { sceneReady.Add(player.ControllerId); return; }
                if (receipt.Status == "hydrated" && !sceneReady.Contains(player.ControllerId))
                {
                    HoldFixture("factory_probe.hydrated_before_scene_ready");
                    return;
                }
            }
            store.RecordReceipt(receipt.OperationId, player.ControllerId, receipt.Status);
            if (receipt.Status == "deployed")
            {
                if (!NativeAssignmentValid) { HoldFixture("native.deployment_without_authority"); return; }
                nativeState.Deploy(player.ControllerId);
            }
            if (receipt.Status.StartsWith("failed:", StringComparison.Ordinal)) HoldFixture(receipt.Status);
            if (receipt.OperationId != store.Current.IncarnationId) return;
            store.RecordReceipt(startOperation, player.ControllerId, receipt.Status);
            if (receipt.Status.StartsWith("failed:", StringComparison.Ordinal)) HoldFixture(receipt.Status);
            if (failure != null || receipt.Status != "hydrated" || !ready.Add(player.ControllerId) || ready.Count != store.Current.Controllers.Length) return;
            if (!hosts.TryGet(store.Current.InstanceId, out var host)) return;
            if (host.Epoch != 1
                || store.Current.Controllers.Any(id => !players.TryGetPeer(id, out _)
                    || (id != host.HostControllerId && !host.SuccessorControllerIds.Contains(id))))
            {
                HoldFixture("factory_probe.assignment_changed_before_release");
                return;
            }
            var release = Guid.NewGuid();
            store.BeginOperation(release, "release");
            foreach (var target in store.Current.Controllers)
                if (players.TryGetPeer(target, out var targetPeer))
                    network.Send(targetPeer, new NetworkNavalLabAction(store.Current.IncarnationId, release, host.Epoch, "release", 0, 0, false));
        }, context: nameof(ReceiveReceipt));
    }

    private void ReceiveFault(MessagePayload<NetworkNavalLabFault> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            if (store.Current?.IncarnationId != payload.What.IncarnationId || payload.Who is not NetPeer peer
                || !players.TryGetPlayer(peer, out var player)
                || !store.IsParticipant(store.Current.InstanceId, player.ControllerId)) return;
            HoldFixture(payload.What.Reason, player.ControllerId);
        }, context: nameof(ReceiveFault));
    }

    private void OnCampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (ModInformation.IsServer && store.CampaignWriteBlocker != null) HoldFixture(store.CampaignWriteBlocker);
    }

    private void HoldFixture(string reason, string origin = "server")
    {
        if (store.Current == null || holdOperation != Guid.Empty) return;
        var operation = Guid.NewGuid();
        store.BeginEmergencyOperation(operation, "hold");
        holdOperation = operation;
        failure = reason;
        // The first hold is the only fault reason recorded on the server.
        Logger.Error("[NavalLabTerminal] {Incarnation} fixture held by {Origin}: {Reason}", store.Current.IncarnationId, origin, reason);
        nativeState.Stop();
        foreach (var target in store.Current.Controllers)
            if (players.TryGetPeer(target, out var peer))
                network.Send(peer, new NetworkNavalLabAction(store.Current.IncarnationId, operation, 0, "hold", 0, 0, false));
    }

    public void Dispose()
    {
        broker.Unsubscribe<NetworkNavalLabShipSample>(ReceiveShipSample);
        broker.Unsubscribe<NetworkNavalLabStations>(ReceiveStations);
        broker.Unsubscribe<NetworkNavalLabHelmOccupancy>(ReceiveHelmOccupancy);
        broker.Unsubscribe<NetworkNavalLabStart>(ReceiveStart);
        broker.Unsubscribe<NetworkNavalLabAction>(ReceiveAction);
        broker.Unsubscribe<NetworkNavalLabReceipt>(ReceiveReceipt);
        broker.Unsubscribe<NetworkNavalLabFault>(ReceiveFault);
        broker.Unsubscribe<NetworkNavalLabRopeFinal>(ReceiveFinalRopes);
        broker.Unsubscribe<CampaignTick>(OnCampaignTick);
        loader.Dispose();
    }
}
#endif
