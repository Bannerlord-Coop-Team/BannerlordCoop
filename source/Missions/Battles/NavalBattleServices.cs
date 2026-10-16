using Common.Messaging;
using GameInterface.Services.ObjectManager;
using System;

namespace Missions.Battles;

/// <summary>
/// [Game thread] The DLC-free ship services of one naval battle mission: hull, station use, weapon and damage traffic and a
/// promoted host's migration reserve. Ticks them in order and disposes them with the mission.
/// </summary>
public interface INavalBattleServices : IDisposable
{
    IBattleShipReplicator ShipReplicator { get; }

    IAgentStationUseReplicator StationUseReplicator { get; }

    IBattleShipWeaponReplicator ShipWeaponReplicator { get; }

    IBattleShipDamageRouter ShipDamageRouter { get; }

    INavalMigrationReserveFielder MigrationReserveFielder { get; }

    /// <summary>
    /// [Game thread] Ticks the hull, station and weapon streams, then <paramref name="beforeMigrationReserve"/>, then the
    /// migration reserve, so AI hulls fielded in between take recovered reserve in the same tick.
    /// </summary>
    void Tick(float dt, Action beforeMigrationReserve = null);
}

/// <inheritdoc cref="INavalBattleServices"/>
public class NavalBattleServices : INavalBattleServices
{
    public NavalBattleServices(IBattleNetwork network, IMessageBroker messageBroker, IBattleTeamResolver teamResolver,
        IObjectManager objectManager, BattleContext context, INavalShipEngine shipEngine)
    {
        ShipReplicator = new BattleShipReplicator(network, messageBroker, context.Session, context.Deployment,
            context.MissionComponent, shipEngine, teamResolver, objectManager, context.HostEpochPolicy, context.ResultCommitter);
        StationUseReplicator = new AgentStationUseReplicator(network, messageBroker, context.Session, context.MissionComponent,
            shipEngine, context.Deployment);
        MigrationReserveFielder = new NavalMigrationReserveFielder(context.Session, context.ReinforcementFielder,
            context.MissionComponent.ShipRegistry, shipEngine, ShipReplicator);
        ShipDamageRouter = new BattleShipDamageRouter(network, messageBroker, context.Session, context.MissionComponent,
            shipEngine);
        ShipWeaponReplicator = new BattleShipWeaponReplicator(network, messageBroker, context.Session, context.MissionComponent,
            shipEngine, ShipReplicator, context.HostEpochPolicy);
    }

    public IBattleShipReplicator ShipReplicator { get; }

    public IAgentStationUseReplicator StationUseReplicator { get; }

    public IBattleShipWeaponReplicator ShipWeaponReplicator { get; }

    public IBattleShipDamageRouter ShipDamageRouter { get; }

    public INavalMigrationReserveFielder MigrationReserveFielder { get; }

    public void Tick(float dt, Action beforeMigrationReserve = null)
    {
        ShipReplicator.Tick(dt);
        StationUseReplicator.Tick(dt);
        ShipWeaponReplicator.Tick(dt);
        beforeMigrationReserve?.Invoke();
        MigrationReserveFielder.Tick();
    }

    public void Dispose()
    {
        ShipWeaponReplicator.Dispose();
        ShipDamageRouter.Dispose();
        StationUseReplicator.Dispose();
        ShipReplicator.Dispose();
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
    private readonly IMessageBroker messageBroker;
    private readonly IBattleTeamResolver teamResolver;
    private readonly IObjectManager objectManager;

    public NavalBattleServicesFactory(IBattleNetwork network, IMessageBroker messageBroker, IBattleTeamResolver teamResolver,
        IObjectManager objectManager)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.teamResolver = teamResolver;
        this.objectManager = objectManager;
    }

    public INavalBattleServices Create(BattleContext context, INavalShipEngine shipEngine) =>
        new NavalBattleServices(network, messageBroker, teamResolver, objectManager, context, shipEngine);
}
