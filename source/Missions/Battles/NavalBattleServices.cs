using Common.Messaging;
using Common.Logging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils;
using Missions.Agents.Handlers;
using Missions.Messages;
using Serilog;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// [Game thread] The DLC-free ship services of one naval battle mission: hull, station use, weapon and damage traffic, the
/// ship fate reports and a promoted host's migration reserve. Ticks them in order and disposes them with the mission.
/// </summary>
public interface INavalBattleServices : IDisposable
{
    /// <summary>Registers, announces and streams the battle's hulls.</summary>
    IBattleShipReplicator ShipReplicator { get; }

    /// <summary>The hulls that left each side, which the end check's retreat rule reads.</summary>
    INavalSideHullLedger HullLedger { get; }

    /// <summary>Replicates in-mission ship captures, confirmed by each captured hull's authority.</summary>
    IBattleShipCaptureReplicator ShipCaptureReplicator { get; }

    /// <summary>Applies the captures the hulls' authorities confirm, on this client's tick.</summary>
    IShipCaptureApplier CaptureApplier { get; }

    /// <summary>The hull and capture traffic that arrived since the last tick, applied first in the next one.</summary>
    IBattleShipArrivals Arrivals { get; }

    /// <summary>Replicates which agent uses which ship station.</summary>
    IAgentStationUseReplicator StationUseReplicator { get; }

    /// <summary>Places agents on the deck of the hull under them.</summary>
    INavalDeckPoses DeckPoses { get; }

    /// <summary>Replicates the ship weapons' state and shots.</summary>
    IBattleShipWeaponReplicator ShipWeaponReplicator { get; }

    /// <summary>Routes ship hits to the hull's authority.</summary>
    IBattleShipDamageRouter ShipDamageRouter { get; }

    /// <summary>Fields a promoted host's recovered reserve onto its hulls.</summary>
    INavalMigrationReserveFielder MigrationReserveFielder { get; }

    /// <summary>Reports the campaign ship fates of the hulls this client simulates to the server.</summary>
    IBattleShipFateReporter FateReporter { get; }

    /// <summary>The battle host's water time every client's waves read.</summary>
    IBattleWaterClock WaterClock { get; }

    /// <summary>
    /// [Game thread] Applies the arrived hull and capture traffic, then ticks the water clock, the hull stream, its captures and
    /// fate reports, the deck, station and weapon streams, then the migration reserve.
    /// </summary>
    void Tick(float dt);

    /// <summary>
    /// [Game thread] Vanilla is about to remove <paramref name="hull"/>: its fate goes to the server and its removal to the
    /// peers (<see cref="IBattleShipReplicator.AnnounceRemoval"/>).
    /// </summary>
    void OnBeforeHullRemoved(MissionObject hull, ShipRemovalReason reason, bool missionEnding);

    /// <summary>
    /// [Game thread] The mission ends while every hull is still live: the own ropes go out once more and the last fates are
    /// flushed, ahead of this client's MissionLeft.
    /// </summary>
    void OnMissionEnding();
}

/// <inheritdoc cref="INavalBattleServices"/>
public class NavalBattleServices : INavalBattleServices
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalBattleServices>();

    private readonly INavalHullAuthority hullAuthority;
    private readonly INavalAppliedHost appliedHost;
    private readonly IShipCaptureApplier captureApplier;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly IAsyncAgentTick asyncAgentTick;
#if DEBUG
    private readonly IShipCaptureWatch captureWatch = new ShipCaptureWatch();
#endif

    public NavalBattleServices(IBattleNetwork network, INetwork relayNetwork, IMessageBroker messageBroker,
        IBattleTeamResolver teamResolver, IObjectManager objectManager, BattleContext context, INavalShipEngine shipEngine,
        INavalShipCaptureEngine captureEngine, IWaterClock waterClock, IAsyncAgentTick asyncAgentTick)
    {
        // These services exist only in a naval mission and open with it.
        context.Session.MarkNaval();
        this.messageBroker = messageBroker;
        this.asyncAgentTick = asyncAgentTick;
        session = context.Session;
        hullAuthority = new NavalHullAuthority(context.Session, context.MissionComponent, shipEngine, context.TroopWithdrawal);
        appliedHost = new NavalAppliedHost(messageBroker, context.Session);
        HullLedger = new NavalSideHullLedger();
        Arrivals = new BattleShipArrivals();
        var conditions = new BattleShipConditionStream(network, context.Session, shipEngine, context.HostEpochPolicy,
            context.MissionContext);
        // A hull leaving with its owner releases the seats on it first, so the stations exist before the hulls.
        StationUseReplicator = new AgentStationUseReplicator(network, messageBroker, context.Session, context.MissionComponent,
            shipEngine, context.Deployment, context.MissionContext);
        ShipReplicator = new BattleShipReplicator(network, messageBroker, context, shipEngine, teamResolver, objectManager,
            hullAuthority, appliedHost, conditions, StationUseReplicator, captureEngine, Arrivals, HullLedger);
        // After the ship replicator, so a spawn record reaches the applier only once the hull it brings spawned.
        captureApplier = new ShipCaptureApplier(network, relayNetwork, messageBroker, context, shipEngine, captureEngine, ShipReplicator,
            hullAuthority, Arrivals, HullLedger);
        ShipCaptureReplicator = new BattleShipCaptureReplicator(network, messageBroker, context, shipEngine, captureEngine,
            hullAuthority, captureApplier, Arrivals);
#if DEBUG
        captureApplier.CaptureApplied += OnCaptureApplied;
#endif
        DeckPoses = new NavalDeckPoses(context.MissionComponent, shipEngine, StationUseReplicator);
        MigrationReserveFielder = new NavalMigrationReserveFielder(context.Session, context.ReinforcementFielder,
            context.MissionComponent.ShipRegistry, shipEngine, hullAuthority);
        ShipDamageRouter = new BattleShipDamageRouter(network, messageBroker, context.Session, context.MissionComponent,
            shipEngine, appliedHost, Arrivals);
        ShipWeaponReplicator = new BattleShipWeaponReplicator(network, messageBroker, context.Session, context.MissionComponent,
            shipEngine, hullAuthority, context.HostEpochPolicy, context.MissionContext);
        FateReporter = new BattleShipFateReporter(relayNetwork, context.Session, context.MissionComponent.ShipRegistry, shipEngine);
        WaterClock = new BattleWaterClock(network, messageBroker, context.Session, context.HostEpochPolicy, waterClock,
            new BattleWaterTimeEstimator());

        messageBroker.Subscribe<BattleResultReporting>(Handle_BattleResultReporting);
    }

    public IBattleShipReplicator ShipReplicator { get; }

    public INavalSideHullLedger HullLedger { get; }

    public IBattleShipCaptureReplicator ShipCaptureReplicator { get; }

    public IShipCaptureApplier CaptureApplier => captureApplier;

    public IBattleShipArrivals Arrivals { get; }

    public IAgentStationUseReplicator StationUseReplicator { get; }

    public INavalDeckPoses DeckPoses { get; }

    public IBattleShipWeaponReplicator ShipWeaponReplicator { get; }

    public IBattleShipDamageRouter ShipDamageRouter { get; }

    public INavalMigrationReserveFielder MigrationReserveFielder { get; }

    public IBattleShipFateReporter FateReporter { get; }

    public IBattleWaterClock WaterClock { get; }

    public void Tick(float dt)
    {
#if DEBUG
        captureWatch.NoteTick();
#endif
        Arrivals.ApplyAll();
        WaterClock.Tick(dt);
        ShipReplicator.Tick(dt);
        captureApplier.Tick(dt);
        ShipCaptureReplicator.Tick(dt);
        FateReporter.Tick(dt);
        DeckPoses.Tick(dt);
        StationUseReplicator.Tick(dt);
        ShipWeaponReplicator.Tick(dt);
        MigrationReserveFielder.Tick();
    }

    public void OnBeforeHullRemoved(MissionObject hull, ShipRemovalReason reason, bool missionEnding)
    {
        FateReporter.ReportRemoval(hull);
        ShipReplicator.AnnounceRemoval(hull, reason, missionEnding);
    }

    // Applies what arrived first, so a capture confirmed this frame still reports its capture and fates before the result.
    public void OnMissionEnding()
    {
        ApplyArrivalsNow();
        ShipReplicator.SendFinalRopes();
        FateReporter.Flush();
    }

    // [Game thread] Published by this client's result committer right before its result report, so the fates ride ahead of it
    // on the relay and the server holds them when it applies the result.
    private void Handle_BattleResultReporting(MessagePayload<BattleResultReporting> payload)
    {
        if (payload.What.InstanceId != session.InstanceId) return;

        ApplyArrivalsNow();
        FateReporter.Flush();
    }

    // Applies what arrived ahead of a result report or the mission end. Outside the mission tick (a result reported from the
    // queue drain or the leave screen) the async agent tick may be running; then nothing waits, and the arrivals stay for the
    // next mission tick, which a continuing mission still runs.
    private void ApplyArrivalsNow()
    {
        if (Arrivals.Count == 0) return;
        if (asyncAgentTick.IsRunning)
        {
            Logger.Debug("[NavalSync] {Count} arrival(s) wait for the next tick: the async agent tick runs", Arrivals.Count);
            return;
        }

        Arrivals.ApplyAll();
    }

#if DEBUG
    // Two live freezes followed a capture within a second, so each capture here is followed by alive lines.
    private void OnCaptureApplied(Guid prizeId, string captor) => captureWatch.Start(prizeId);
#endif

    public void Dispose()
    {
        // First, so a throwing service below can't leave the engine reading battle water time on the campaign map.
        WaterClock.Dispose();
#if DEBUG
        captureWatch.Dispose();
        captureApplier.CaptureApplied -= OnCaptureApplied;
#endif
        messageBroker.Unsubscribe<BattleResultReporting>(Handle_BattleResultReporting);
        ShipWeaponReplicator.Dispose();
        ShipDamageRouter.Dispose();
        DeckPoses.Dispose();
        StationUseReplicator.Dispose();
        ShipCaptureReplicator.Dispose();
        captureApplier.Dispose();
        ShipReplicator.Dispose();
        hullAuthority.Dispose();
        appliedHost.Dispose();
        Arrivals.Clear();
    }
}

/// <summary>Builds the <see cref="INavalBattleServices"/> of one naval battle on its controller's context.</summary>
public interface INavalBattleServicesFactory
{
    INavalBattleServices Create(BattleContext context, INavalShipEngine shipEngine, INavalShipCaptureEngine captureEngine,
        IAsyncAgentTick asyncAgentTick);
}

/// <inheritdoc cref="INavalBattleServicesFactory"/>
public class NavalBattleServicesFactory : INavalBattleServicesFactory
{
    private readonly IBattleNetwork network;
    private readonly INetwork relayNetwork;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleTeamResolver teamResolver;
    private readonly IObjectManager objectManager;
    private readonly IWaterClock waterClock;

    public NavalBattleServicesFactory(IBattleNetwork network, INetwork relayNetwork, IMessageBroker messageBroker,
        IBattleTeamResolver teamResolver, IObjectManager objectManager, IWaterClock waterClock)
    {
        this.network = network;
        this.relayNetwork = relayNetwork;
        this.messageBroker = messageBroker;
        this.teamResolver = teamResolver;
        this.objectManager = objectManager;
        this.waterClock = waterClock;
    }

    public INavalBattleServices Create(BattleContext context, INavalShipEngine shipEngine, INavalShipCaptureEngine captureEngine,
        IAsyncAgentTick asyncAgentTick)
    {
        // Installed on first use: only a client opening a naval battle gets here, and the patch refuses any server anyway.
        waterClock.TryInstall();
        return new NavalBattleServices(network, relayNetwork, messageBroker, teamResolver, objectManager, context, shipEngine,
            captureEngine, waterClock, asyncAgentTick);
    }
}
