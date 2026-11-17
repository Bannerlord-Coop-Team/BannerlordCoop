using Common;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using Missions;
using Missions.Battles;
using Missions.Services.Network;
using Moq;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// Builds the ship replicator over the hull core it composes, the way <see cref="NavalBattleServices"/> does, so the replicator
/// tests construct it in one place.
/// </summary>
internal sealed class ShipReplicatorFixture
{
    // Composes the hull core on the context's session, mission component and epoch policy; unset collaborators are mocks.
    public ShipReplicatorFixture(BattleContext context, IMessageBroker broker, INavalShipEngine engine, IBattleNetwork network = null,
        IBattleTeamResolver teamResolver = null, IAgentStationUseReplicator stations = null,
        INavalShipCaptureEngine captureEngine = null, IBattleShipArrivals arrivals = null, IObjectManager objectManager = null)
    {
        network ??= Mock.Of<IBattleNetwork>();
        Arrivals = arrivals ?? new ImmediateShipArrivals();
        HullAuthority = new NavalHullAuthority(context.Session, context.MissionComponent, engine, context.TroopWithdrawal);
        AppliedHost = new NavalAppliedHost(broker, context.Session);
        Ledger = new NavalSideHullLedger();
        HeldShips = new HeldShipBuffer();
        Conditions = new BattleShipConditionStream(network, broker, context, engine, AppliedHost, HeldShips, Arrivals);
        Samples = new BattleShipSampleStream(network, broker, context, engine, Conditions, HeldShips, Arrivals);
        Replicator = new BattleShipReplicator(network, broker, context, engine, teamResolver ?? Mock.Of<IBattleTeamResolver>(),
            objectManager ?? Mock.Of<IObjectManager>(), HullAuthority, AppliedHost, Conditions, stations ?? Mock.Of<IAgentStationUseReplicator>(),
            captureEngine ?? Mock.Of<INavalShipCaptureEngine>(), Arrivals, Ledger, HeldShips, Samples);
    }

    /// <summary>The arrivals queue the replicator enqueues on; applies at once unless the test passed a real queue.</summary>
    public IBattleShipArrivals Arrivals { get; }

    /// <summary>Moves hull authority, swaps and removes hulls.</summary>
    public NavalHullAuthority HullAuthority { get; }

    /// <summary>The host and epoch whose AI hull move this client applied.</summary>
    public NavalAppliedHost AppliedHost { get; }

    /// <summary>The hulls that left each side.</summary>
    public NavalSideHullLedger Ledger { get; }

    /// <summary>What arrived for ships not registered yet.</summary>
    public HeldShipBuffer HeldShips { get; }

    /// <summary>The hulls' damage state and reported reserve.</summary>
    public BattleShipConditionStream Conditions { get; }

    /// <summary>The hulls' 20 Hz samples.</summary>
    public BattleShipSampleStream Samples { get; }

    /// <summary>The replicator under test.</summary>
    public BattleShipReplicator Replicator { get; }

    /// <summary>
    /// A battle context over the given session, mission component and members, with a fresh epoch policy; the rest are mocks
    /// unless given.
    /// </summary>
    public static BattleContext Context(IBattleSession session, ICoopMissionComponent missionComponent, IMissionContext members,
        IBattleDeploymentCoordinator deployment = null, IBattleResultCommitter resultCommitter = null,
        IBattlePartyWithdrawal partyWithdrawal = null) =>
        new BattleContext(session, deployment ?? Mock.Of<IBattleDeploymentCoordinator>(), missionComponent, new HostEpochPolicy(),
            resultCommitter ?? Mock.Of<IBattleResultCommitter>(), Mock.Of<IReinforcementFielder>(), members,
            Mock.Of<IBattleTroopWithdrawal>(), partyWithdrawal ?? Mock.Of<IBattlePartyWithdrawal>());

    /// <summary>Publishes a message whose handler hops to the game thread, and waits until that hop ran.</summary>
    public static void PublishAndDrain<T>(IMessageBroker broker, object source, T message) where T : IMessage
    {
        broker.Publish(source, message);
        GameThread.Run(() => { }, blocking: true);
    }
}
