using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using Missions.Agents.Handlers;
using System;

namespace Missions.Battles;

/// <summary>
/// [Game thread] The DLC-free ship services of one naval battle mission: hull, station use, weapon and damage traffic, the
/// ship fate reports and a promoted host's migration reserve. Ticks them in order and disposes them with the mission.
/// </summary>
public interface INavalBattleServices : IDisposable
{
    /// <summary>The hull operations the services share; its copies are stepped on the fixed physics tick.</summary>
    INavalShipEngine Engine { get; }

    IBattleShipReplicator ShipReplicator { get; }

    IAgentStationUseReplicator StationUseReplicator { get; }

    INavalDeckPoses DeckPoses { get; }

    IBattleShipWeaponReplicator ShipWeaponReplicator { get; }

    IBattleShipDamageRouter ShipDamageRouter { get; }

    INavalMigrationReserveFielder MigrationReserveFielder { get; }

    /// <summary>Reports the campaign ship fates of the hulls this client simulates to the server.</summary>
    IBattleShipFateReporter FateReporter { get; }

    /// <summary>
    /// [Game thread] Ticks the hull stream and its fate reports, the deck, station and weapon streams, then
    /// <paramref name="beforeMigrationReserve"/>, then the migration reserve, so AI hulls fielded in between take recovered
    /// reserve in the same tick.
    /// </summary>
    void Tick(float dt, Action beforeMigrationReserve = null);
}

/// <inheritdoc cref="INavalBattleServices"/>
public class NavalBattleServices : INavalBattleServices
{
    private readonly INavalHullAuthority hullAuthority;
    private readonly IBattleResultCommitter resultCommitter;

    public NavalBattleServices(IBattleNetwork network, INetwork relayNetwork, IMessageBroker messageBroker,
        IBattleTeamResolver teamResolver, IObjectManager objectManager, BattleContext context, INavalShipEngine shipEngine)
    {
        // These services exist only in a naval mission and open with it.
        context.Session.MarkNaval();
        Engine = shipEngine;
        hullAuthority = new NavalHullAuthority(context.Session, context.MissionComponent, shipEngine, context.TroopWithdrawal);
        var conditions = new BattleShipConditionStream(network, context.Session, shipEngine, context.HostEpochPolicy,
            context.MissionContext);
        // A hull leaving with its owner releases the seats on it first, so the stations exist before the hulls.
        StationUseReplicator = new AgentStationUseReplicator(network, messageBroker, context.Session, context.MissionComponent,
            shipEngine, context.Deployment, context.MissionContext);
        ShipReplicator = new BattleShipReplicator(network, messageBroker, context, shipEngine, teamResolver, objectManager,
            hullAuthority, conditions, StationUseReplicator);
        DeckPoses = new NavalDeckPoses(context.MissionComponent, shipEngine, StationUseReplicator);
        MigrationReserveFielder = new NavalMigrationReserveFielder(context.Session, context.ReinforcementFielder,
            context.MissionComponent.ShipRegistry, shipEngine, ShipReplicator);
        ShipDamageRouter = new BattleShipDamageRouter(network, messageBroker, context.Session, context.MissionComponent,
            shipEngine);
        ShipWeaponReplicator = new BattleShipWeaponReplicator(network, messageBroker, context.Session, context.MissionComponent,
            shipEngine, ShipReplicator, context.HostEpochPolicy, context.MissionContext);
        FateReporter = new BattleShipFateReporter(relayNetwork, context.Session, context.MissionComponent.ShipRegistry, shipEngine);

        // The fates ride ahead of every result report on the relay, so the server holds them when it applies the result.
        resultCommitter = context.ResultCommitter;
        resultCommitter.SetBeforeReport(FateReporter.Flush);
    }

    public INavalShipEngine Engine { get; }

    public IBattleShipReplicator ShipReplicator { get; }

    public IAgentStationUseReplicator StationUseReplicator { get; }

    public INavalDeckPoses DeckPoses { get; }

    public IBattleShipWeaponReplicator ShipWeaponReplicator { get; }

    public IBattleShipDamageRouter ShipDamageRouter { get; }

    public INavalMigrationReserveFielder MigrationReserveFielder { get; }

    public IBattleShipFateReporter FateReporter { get; }

    public void Tick(float dt, Action beforeMigrationReserve = null)
    {
        ShipReplicator.Tick(dt);
        FateReporter.Tick(dt);
        DeckPoses.Tick(dt);
        StationUseReplicator.Tick(dt);
        ShipWeaponReplicator.Tick(dt);
        beforeMigrationReserve?.Invoke();
        MigrationReserveFielder.Tick();
    }

    public void Dispose()
    {
        resultCommitter.SetBeforeReport(null);
        ShipWeaponReplicator.Dispose();
        ShipDamageRouter.Dispose();
        DeckPoses.Dispose();
        StationUseReplicator.Dispose();
        ShipReplicator.Dispose();
        hullAuthority.Dispose();
    }
}

/// <summary>Builds the <see cref="INavalBattleServices"/> of one naval battle on its controller's context.</summary>
public interface INavalBattleServicesFactory
{
    INavalBattleServices Create(BattleContext context, INavalShipEngine shipEngine);
}

/// <inheritdoc cref="INavalBattleServicesFactory"/>
public class NavalBattleServicesFactory : INavalBattleServicesFactory
{
    private readonly IBattleNetwork network;
    private readonly INetwork relayNetwork;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleTeamResolver teamResolver;
    private readonly IObjectManager objectManager;

    public NavalBattleServicesFactory(IBattleNetwork network, INetwork relayNetwork, IMessageBroker messageBroker,
        IBattleTeamResolver teamResolver, IObjectManager objectManager)
    {
        this.network = network;
        this.relayNetwork = relayNetwork;
        this.messageBroker = messageBroker;
        this.teamResolver = teamResolver;
        this.objectManager = objectManager;
    }

    public INavalBattleServices Create(BattleContext context, INavalShipEngine shipEngine) =>
        new NavalBattleServices(network, relayNetwork, messageBroker, teamResolver, objectManager, context, shipEngine);
}
