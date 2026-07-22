using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.Players;
using Missions.Messages;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class BattlePrioritySlotServerFlowTests : MissionTestEnvironment
{
    private const string HostController = "priority-host";
    private const string DonorController = "priority-donor";
    private const string WaitingController = "priority-waiting";
    private const string NextWaitingController = "priority-next-waiting";

    private readonly struct PriorityScenario
    {
        public readonly string MapEventId;
        public readonly string DonorPartyId;
        public readonly string WaitingPartyId;

        public PriorityScenario(string mapEventId, string donorPartyId, string waitingPartyId)
        {
            MapEventId = mapEventId;
            DonorPartyId = donorPartyId;
            WaitingPartyId = waitingPartyId;
        }
    }

    private readonly struct DeferredPriorityScenario
    {
        public readonly string MapEventId;
        public readonly string DonorPartyId;
        public readonly string WaitingMobilePartyId;

        public DeferredPriorityScenario(
            string mapEventId,
            string donorPartyId,
            string waitingMobilePartyId)
        {
            MapEventId = mapEventId;
            DonorPartyId = donorPartyId;
            WaitingMobilePartyId = waitingMobilePartyId;
        }
    }

    public BattlePrioritySlotServerFlowTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void FreedSlot_RefreshesBeforeAssignment_AndDepartureRestoresTheDonor()
    {
        var scenario = CreateFullBattleWithWaitingPlayer();
        var clients = Clients.ToArray();
        EnterBattle(clients[0], scenario.MapEventId);
        EnterBattle(clients[1], scenario.MapEventId);
        EnterBattle(clients[2], scenario.MapEventId);

        Server.NetworkSentMessages.Clear();
        PublishFreedSlot(scenario, troopSeed: 1001);

        var sent = Server.NetworkSentMessages.Messages;
        int assignmentIndex = IndexOf<NetworkBattlePrioritySlotAssigned>(sent);
        Assert.True(assignmentIndex > 0);
        Assert.All(sent.Take(assignmentIndex), message => Assert.IsType<NetworkBattleTroopReserve>(message));
        var assignment = Assert.IsType<NetworkBattlePrioritySlotAssigned>(sent[assignmentIndex]);
        Assert.Equal(scenario.WaitingPartyId, assignment.WaitingPartyId);
        Assert.Equal(scenario.DonorPartyId, assignment.DonorPartyId);
        Assert.Equal(1, GetInitialSpawnCount(scenario, WaitingController, scenario.WaitingPartyId));
        Assert.Equal(0, GetInitialSpawnCount(scenario, DonorController, scenario.DonorPartyId));

        Server.NetworkSentMessages.Clear();
        PublishFreedSlot(scenario, troopSeed: 1001);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBattlePrioritySlotAssigned>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBattleTroopReserve>());

        Server.NetworkSentMessages.Clear();
        DepartBattle(WaitingController, scenario.MapEventId);

        sent = Server.NetworkSentMessages.Messages;
        int cancellationIndex = IndexOf<NetworkBattlePrioritySlotCancelled>(sent);
        Assert.True(cancellationIndex > 0);
        Assert.All(sent.Take(cancellationIndex), message => Assert.IsType<NetworkBattleTroopReserve>(message));
        Assert.DoesNotContain(sent.Skip(cancellationIndex + 1), message => message is NetworkBattleTroopReserve);
        var cancellation = Assert.IsType<NetworkBattlePrioritySlotCancelled>(sent[cancellationIndex]);
        Assert.Equal(assignment.TransferId, cancellation.TransferId);
        Assert.Equal(scenario.WaitingPartyId, cancellation.WaitingPartyId);
        Assert.Equal(scenario.DonorPartyId, cancellation.DonorPartyId);
        Assert.Equal(1, GetInitialSpawnCount(scenario, DonorController, scenario.DonorPartyId));
    }

    [Fact]
    public void FreedSlotWithoutWaiter_LatePlayerClaimsRetainedVacancyAfterReserveRefresh()
    {
        var scenario = CreateFullBattleWithDeferredWaitingPlayer();
        var clients = Clients.ToArray();
        EnterBattle(clients[0], scenario.MapEventId);
        EnterBattle(clients[1], scenario.MapEventId);

        Server.NetworkSentMessages.Clear();
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(
            this,
            new BattleHumanSlotFreed(
                scenario.MapEventId,
                scenario.DonorPartyId,
                troopSeed: 1101)));

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBattlePrioritySlotAssigned>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBattleTroopReserve>());

        Server.NetworkSentMessages.Clear();
        string waitingPartyId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(scenario.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                scenario.WaitingMobilePartyId, out var waitingParty));
            var waitingTroop = Server.CreateRegisteredObject<CharacterObject>("retained_flow_waiting_troop");

            waitingParty.Party.MemberRoster.AddToCounts(waitingTroop, 1);
            waitingParty.Party.MapEventSide = mapEvent.AttackerSide;
            var waitingMapEventParty = mapEvent.AttackerSide.Parties.Single(
                party => party.Party == waitingParty.Party);
            waitingMapEventParty.Update();
            Assert.True(Server.ObjectManager.TryGetId(waitingMapEventParty, out waitingPartyId));
        });

        Assert.NotNull(waitingPartyId);
        var sent = Server.NetworkSentMessages.Messages;
        int assignmentIndex = IndexOf<NetworkBattlePrioritySlotAssigned>(sent);
        int reserveIndex = IndexOf<NetworkBattleTroopReserve>(sent);
        Assert.True(assignmentIndex > 0);
        Assert.InRange(reserveIndex, 0, assignmentIndex - 1);
        var assignment = Assert.IsType<NetworkBattlePrioritySlotAssigned>(sent[assignmentIndex]);
        Assert.Equal(scenario.DonorPartyId, assignment.DonorPartyId);
        Assert.Equal(waitingPartyId, assignment.WaitingPartyId);

        var assignedScenario = new PriorityScenario(
            scenario.MapEventId,
            scenario.DonorPartyId,
            waitingPartyId);
        Assert.Equal(0, GetInitialSpawnCount(
            assignedScenario,
            DonorController,
            scenario.DonorPartyId));
        Assert.Equal(1, GetInitialSpawnCount(
            assignedScenario,
            WaitingController,
            waitingPartyId));
    }

    [Fact]
    public void WasRetreatDeparture_RetainsDepartingPartySlotForWaitingPlayer()
    {
        var scenario = CreateFullBattleWithWaitingPlayer();
        var clients = Clients.ToArray();
        EnterBattle(clients[0], scenario.MapEventId);
        EnterBattle(clients[1], scenario.MapEventId);
        EnterBattle(clients[2], scenario.MapEventId);

        Server.NetworkSentMessages.Clear();
        DepartBattle(DonorController, scenario.MapEventId, wasRetreat: true);

        var sent = Server.NetworkSentMessages.Messages;
        int assignmentIndex = IndexOf<NetworkBattlePrioritySlotAssigned>(sent);
        Assert.True(assignmentIndex > 0);
        Assert.Contains(sent.Take(assignmentIndex), message => message is NetworkBattleTroopReserve);
        var assignment = Assert.IsType<NetworkBattlePrioritySlotAssigned>(sent[assignmentIndex]);
        Assert.Equal(scenario.DonorPartyId, assignment.DonorPartyId);
        Assert.Equal(scenario.WaitingPartyId, assignment.WaitingPartyId);
        Assert.Equal(0, GetInitialSpawnCount(
            scenario,
            DonorController,
            scenario.DonorPartyId));
        Assert.Equal(1, GetInitialSpawnCount(
            scenario,
            WaitingController,
            scenario.WaitingPartyId));
    }

    [Fact]
    public void LoadingDisconnectBeforeReserveRequest_CancelsThenReconnectRequeuesTheWait()
    {
        var scenario = CreateFullBattleWithWaitingPlayer();
        var clients = Clients.ToArray();
        var waitingClient = clients[2];
        EnterBattle(clients[0], scenario.MapEventId);
        EnterBattle(clients[1], scenario.MapEventId);

        Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer(WaitingController, waitingClient.NetPeer));
        Server.NetworkSentMessages.Clear();
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(
            this,
            new PlayerDisconnected(waitingClient.NetPeer, default)));

        var cancellation = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkBattlePrioritySlotCancelled>());
        Assert.Equal(0, cancellation.TransferId);
        Assert.Equal(scenario.WaitingPartyId, cancellation.WaitingPartyId);
        Assert.Null(cancellation.DonorPartyId);

        Server.NetworkSentMessages.Clear();
        waitingClient.Call(() => waitingClient.Resolve<INetwork>().SendAll(
            new NetworkRequestBattleReserves(scenario.MapEventId, WaitingController)));

        var sent = Server.NetworkSentMessages.Messages;
        int queuedIndex = IndexOf<NetworkBattlePriorityWaitQueued>(sent);
        int firstReserveIndex = IndexOf<NetworkBattleTroopReserve>(sent);
        Assert.True(queuedIndex >= 0);
        Assert.True(firstReserveIndex > queuedIndex);
        var queued = Assert.IsType<NetworkBattlePriorityWaitQueued>(sent[queuedIndex]);
        Assert.Equal(scenario.WaitingPartyId, queued.WaitingPartyId);
        Assert.True(queued.ResetExistingState);
        var queuedSnapshot = sent.OfType<NetworkBattlePriorityWaitQueued>().ToArray();
        Assert.Equal(3, queuedSnapshot.Length);
        Assert.All(queuedSnapshot.Skip(1), message => Assert.False(message.ResetExistingState));

        Server.NetworkSentMessages.Clear();
        PublishFreedSlot(scenario, troopSeed: 1002);
        var assignment = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkBattlePrioritySlotAssigned>());
        Assert.Equal(scenario.WaitingPartyId, assignment.WaitingPartyId);
    }

    [Fact]
    public void ConsumedSlot_ReassignsTheSameTransferWhenThePlayerLaterDeparts()
    {
        var scenario = CreateFullBattleWithWaitingPlayer();
        var nextWaitingPartyId = AddWaitingPlayer(scenario, NextWaitingController);
        var clients = Clients.ToArray();
        EnterBattle(clients[0], scenario.MapEventId);
        EnterBattle(clients[1], scenario.MapEventId);
        EnterBattle(clients[2], scenario.MapEventId);

        Server.NetworkSentMessages.Clear();
        PublishFreedSlot(scenario, troopSeed: 1003);
        var assignment = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkBattlePrioritySlotAssigned>());

        clients[2].Call(() => clients[2].Resolve<INetwork>().SendAll(
            new NetworkBattlePrioritySlotConsumed(
                scenario.MapEventId,
                assignment.TransferId,
                scenario.WaitingPartyId)));

        var consumed = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkBattlePrioritySlotConsumed>());
        Assert.Equal(assignment.TransferId, consumed.TransferId);
        Assert.Equal(scenario.WaitingPartyId, consumed.WaitingPartyId);

        Server.NetworkSentMessages.Clear();
        DepartBattle(WaitingController, scenario.MapEventId);

        var sent = Server.NetworkSentMessages.Messages;
        int reassignmentIndex = IndexOf<NetworkBattlePrioritySlotAssigned>(sent);
        Assert.True(reassignmentIndex > 0);
        Assert.All(sent.Take(reassignmentIndex), message => Assert.IsType<NetworkBattleTroopReserve>(message));
        var reassignment = Assert.IsType<NetworkBattlePrioritySlotAssigned>(sent[reassignmentIndex]);
        Assert.Equal(assignment.TransferId, reassignment.TransferId);
        Assert.Equal(nextWaitingPartyId, reassignment.WaitingPartyId);
        Assert.Equal(scenario.DonorPartyId, reassignment.DonorPartyId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBattlePrioritySlotCancelled>());
        Assert.Equal(0, GetInitialSpawnCount(scenario, DonorController, scenario.DonorPartyId));
        Assert.Equal(0, GetInitialSpawnCount(scenario, WaitingController, scenario.WaitingPartyId));
        Assert.Equal(1, GetInitialSpawnCount(scenario, NextWaitingController, nextWaitingPartyId));
    }

    [Fact]
    public void IncumbentReserveReentry_ReplaysConsumedTransferBeforeAndAfterReserves()
    {
        var scenario = CreateFullBattleWithWaitingPlayer();
        var clients = Clients.ToArray();
        EnterBattle(clients[0], scenario.MapEventId);
        EnterBattle(clients[1], scenario.MapEventId);
        EnterBattle(clients[2], scenario.MapEventId);

        Server.NetworkSentMessages.Clear();
        PublishFreedSlot(scenario, troopSeed: 1004);
        var transfer = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkBattlePrioritySlotAssigned>());
        clients[2].Call(() => clients[2].Resolve<INetwork>().SendAll(
            new NetworkBattlePrioritySlotConsumed(
                scenario.MapEventId,
                transfer.TransferId,
                scenario.WaitingPartyId)));

        // Model an incumbent reconnecting without any of the earlier priority broadcasts.
        Server.NetworkSentMessages.Clear();
        clients[1].InternalMessages.Clear();
        clients[1].Call(() => clients[1].Resolve<INetwork>().SendAll(
            new NetworkRequestBattleReserves(scenario.MapEventId, DonorController)));

        var sent = clients[1].InternalMessages.Messages;
        Assert.Collection(
            sent,
            message => Assert.Equal(scenario.MapEventId,
                Assert.IsType<NetworkBattlePrioritySnapshotReset>(message).MapEventId),
            message => AssertPriorityAssignment(message, transfer),
            message => AssertPriorityConsumed(message, transfer),
            message => Assert.IsType<NetworkBattleTroopReserve>(message),
            message => AssertPriorityAssignment(message, transfer),
            message => AssertPriorityConsumed(message, transfer));
    }

    [Fact]
    public void ConsumedHumanDepartureWithoutWaiter_SettlesReplayButRetainsPartyCleanupChain()
    {
        var scenario = CreateFullBattleWithWaitingPlayer();
        var clients = Clients.ToArray();
        EnterBattle(clients[0], scenario.MapEventId);
        EnterBattle(clients[1], scenario.MapEventId);
        EnterBattle(clients[2], scenario.MapEventId);

        Server.NetworkSentMessages.Clear();
        PublishFreedSlot(scenario, troopSeed: 1005);
        var transfer = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkBattlePrioritySlotAssigned>());
        clients[2].Call(() => clients[2].Resolve<INetwork>().SendAll(
            new NetworkBattlePrioritySlotConsumed(
                scenario.MapEventId,
                transfer.TransferId,
                scenario.WaitingPartyId)));

        Server.NetworkSentMessages.Clear();
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(
            this,
            new BattleHumanSlotFreed(
                scenario.MapEventId,
                scenario.WaitingPartyId,
                troopSeed: 1006)));

        var settled = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkBattlePrioritySlotSettled>());
        Assert.Equal(transfer.TransferId, settled.TransferId);
        Assert.Equal(scenario.WaitingPartyId, settled.WaitingPartyId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBattlePrioritySlotAssigned>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBattleTroopReserve>());

        clients[1].InternalMessages.Clear();
        clients[1].Call(() => clients[1].Resolve<INetwork>().SendAll(
            new NetworkRequestBattleReserves(scenario.MapEventId, DonorController)));
        Assert.Collection(
            clients[1].InternalMessages.Messages,
            message => Assert.Equal(scenario.MapEventId,
                Assert.IsType<NetworkBattlePrioritySnapshotReset>(message).MapEventId),
            message => Assert.IsType<NetworkBattleTroopReserve>(message));

        Server.NetworkSentMessages.Clear();
        DepartBattle(WaitingController, scenario.MapEventId);
        var cancellation = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkBattlePrioritySlotCancelled>());
        Assert.Equal(transfer.TransferId, cancellation.TransferId);
        Assert.Equal(scenario.WaitingPartyId, cancellation.WaitingPartyId);
        Assert.Equal(scenario.DonorPartyId, cancellation.DonorPartyId);
        Assert.Equal(1, GetInitialSpawnCount(
            scenario,
            DonorController,
            scenario.DonorPartyId));
    }

    private DeferredPriorityScenario CreateFullBattleWithDeferredWaitingPlayer()
    {
        var (mapEventId, _) = SetupCoopBattle(HostController, DonorController);
        var clients = Clients.ToArray();
        SetControllerId(clients[2], WaitingController);

        var waitingHeroId = CreateRegisteredObject<Hero>();
        var waitingMobilePartyId = CreateRegisteredObject<MobileParty>();
        RegisterAsPlayerParty(WaitingController, waitingHeroId, waitingMobilePartyId);

        string donorPartyId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var initialTroop = Server.CreateRegisteredObject<CharacterObject>("deferred_priority_initial_troop");

            foreach (var party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                party.Party.MemberRoster.Clear();
                party.Party.MemberRoster.AddToCounts(initialTroop, 1);
                party.Update();
            }

            Server.Resolve<IBattleTroopReserveBuilder>().PreparePlan(mapEvent, battleSize: 2);
            var donor = mapEvent.DefenderSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(donor, out donorPartyId));
        });

        Assert.NotNull(donorPartyId);
        return new DeferredPriorityScenario(mapEventId, donorPartyId, waitingMobilePartyId);
    }

    private PriorityScenario CreateFullBattleWithWaitingPlayer()
    {
        var (mapEventId, _) = SetupCoopBattle(HostController, DonorController);
        var clients = Clients.ToArray();
        SetControllerId(clients[2], WaitingController);

        var waitingHeroId = CreateRegisteredObject<Hero>();
        var waitingMobilePartyId = CreateRegisteredObject<MobileParty>();
        RegisterAsPlayerParty(WaitingController, waitingHeroId, waitingMobilePartyId);

        string donorPartyId = null;
        string waitingPartyId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(waitingMobilePartyId, out var waitingParty));
            var initialTroop = Server.CreateRegisteredObject<CharacterObject>("priority_initial_troop");
            var waitingTroop = Server.CreateRegisteredObject<CharacterObject>("priority_waiting_troop");

            foreach (var party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                party.Party.MemberRoster.Clear();
                party.Party.MemberRoster.AddToCounts(initialTroop, 1);
                party.Update();
            }

            var reserveBuilder = Server.Resolve<IBattleTroopReserveBuilder>();
            reserveBuilder.PreparePlan(mapEvent, battleSize: 2);

            waitingParty.Party.MemberRoster.AddToCounts(waitingTroop, 1);
            waitingParty.Party.MapEventSide = mapEvent.AttackerSide;
            var waitingMapEventParty = mapEvent.AttackerSide.Parties.Single(
                party => party.Party == waitingParty.Party);
            waitingMapEventParty.Update();
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent,
                waitingMapEventParty,
                out _,
                out var waitsForPrioritySlot));
            Assert.True(waitsForPrioritySlot);

            var donor = mapEvent.DefenderSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(donor, out donorPartyId));
            Assert.True(Server.ObjectManager.TryGetId(waitingMapEventParty, out waitingPartyId));
        });

        Assert.NotNull(donorPartyId);
        Assert.NotNull(waitingPartyId);
        return new PriorityScenario(mapEventId, donorPartyId, waitingPartyId);
    }

    private string AddWaitingPlayer(PriorityScenario scenario, string controllerId)
    {
        var waitingHeroId = CreateRegisteredObject<Hero>();
        var waitingMobilePartyId = CreateRegisteredObject<MobileParty>();
        RegisterAsPlayerParty(controllerId, waitingHeroId, waitingMobilePartyId);

        string waitingPartyId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(scenario.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(waitingMobilePartyId, out var waitingParty));
            var waitingTroop = Server.CreateRegisteredObject<CharacterObject>(controllerId + "_troop");

            waitingParty.Party.MemberRoster.AddToCounts(waitingTroop, 1);
            waitingParty.Party.MapEventSide = mapEvent.AttackerSide;
            var waitingMapEventParty = mapEvent.AttackerSide.Parties.Single(
                party => party.Party == waitingParty.Party);
            waitingMapEventParty.Update();

            Assert.Equal(0, Server.Resolve<IBattleTroopReserveBuilder>().GrantUnassignedInitialSpawns(
                mapEvent,
                waitingMapEventParty,
                out _,
                out var waitsForPrioritySlot));
            Assert.True(waitsForPrioritySlot);
            Assert.True(Server.ObjectManager.TryGetId(waitingMapEventParty, out waitingPartyId));
        });

        Assert.NotNull(waitingPartyId);
        return waitingPartyId;
    }

    private void PublishFreedSlot(PriorityScenario scenario, int troopSeed)
    {
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(
            this,
            new BattleHumanSlotFreed(scenario.MapEventId, scenario.DonorPartyId, troopSeed)));
    }

    private int GetInitialSpawnCount(PriorityScenario scenario, string controllerId, string partyId)
    {
        int initialSpawnCount = -1;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(scenario.MapEventId, out var mapEvent));
            initialSpawnCount = Server.Resolve<IBattleTroopReserveBuilder>()
                .GetOwnedReserves(mapEvent, controllerId, isHost: false)
                .SelectMany(side => side.Parties)
                .Single(party => party.PartyId == partyId)
                .InitialSpawnCount;
        });
        return initialSpawnCount;
    }

    private static int IndexOf<T>(IReadOnlyList<IMessage> messages) where T : IMessage
    {
        for (int i = 0; i < messages.Count; i++)
            if (messages[i] is T)
                return i;
        return -1;
    }

    private static void AssertPriorityAssignment(
        IMessage message,
        NetworkBattlePrioritySlotAssigned expected)
    {
        var assignment = Assert.IsType<NetworkBattlePrioritySlotAssigned>(message);
        Assert.Equal(expected.MapEventId, assignment.MapEventId);
        Assert.Equal(expected.TransferId, assignment.TransferId);
        Assert.Equal(expected.WaitingPartyId, assignment.WaitingPartyId);
        Assert.Equal(expected.DonorPartyId, assignment.DonorPartyId);
    }

    private static void AssertPriorityConsumed(
        IMessage message,
        NetworkBattlePrioritySlotAssigned expected)
    {
        var consumed = Assert.IsType<NetworkBattlePrioritySlotConsumed>(message);
        Assert.Equal(expected.MapEventId, consumed.MapEventId);
        Assert.Equal(expected.TransferId, consumed.TransferId);
        Assert.Equal(expected.WaitingPartyId, consumed.WaitingPartyId);
    }
}
