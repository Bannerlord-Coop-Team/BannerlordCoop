using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using Missions.Battles;
using Missions.Messages;
using SandBox.GauntletUI.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// #3989: the hideout boss duel runs on the battle host with its own main agent, so the host must be
/// the player who leads the assault, not whichever player finished loading first.
/// </summary>
public class HideoutAssaultHostElectionTests : MissionTestEnvironment
{
    private const string LeaderControllerId = "ctrl-Leader";
    private const string AllyControllerId = "ctrl-Ally";
    private const string SecondAllyControllerId = "ctrl-SecondAlly";

    public HideoutAssaultHostElectionTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void AllyLoadsBeforeAssaultLeader_LeaderBecomesHost()
    {
        var mapEventId = SetupHideoutAssault();
        var clients = Clients.ToArray();

        EnterBattle(clients[1], mapEventId);
        EnterBattle(clients[0], mapEventId);

        foreach (var instance in Clients.Append(Server))
            AssertHostController(instance, mapEventId, LeaderControllerId);
        AssertIsLocalHost(clients[0], mapEventId, true);
        AssertIsLocalHost(clients[1], mapEventId, false);

        // The waiting ally joins the successor line once it has the leader's hideout state
        SendHideoutStateReady(clients[1], AllyControllerId, mapEventId);
        AssertHost(Server, mapEventId, LeaderControllerId, AllyControllerId);
    }

    [Fact]
    public void AssaultLeaderLoadsFirst_LeaderBecomesHost()
    {
        var mapEventId = SetupHideoutAssault();
        var clients = Clients.ToArray();

        EnterBattle(clients[0], mapEventId);
        EnterBattle(clients[1], mapEventId);

        foreach (var instance in Clients.Append(Server))
            AssertHostController(instance, mapEventId, LeaderControllerId);
        AssertIsLocalHost(clients[0], mapEventId, true);
        AssertIsLocalHost(clients[1], mapEventId, false);
    }

    [Fact]
    public void AssaultLeaderNotConnected_FirstReadyPlayerHosts()
    {
        var mapEventId = SetupHideoutAssault(leaderConnected: false);
        var clients = Clients.ToArray();

        EnterBattle(clients[1], mapEventId);

        foreach (var instance in Clients.Append(Server))
            AssertHostController(instance, mapEventId, AllyControllerId);
    }

    [Fact]
    public void AssaultLeaderAbsent_FirstReadyPlayerHostsWithoutWaiting()
    {
        var mapEventId = SetupHideoutAssault();
        var clients = Clients.ToArray();

        EnterBattle(clients[0], mapEventId, missionReady: false);
        DepartBattle(LeaderControllerId, mapEventId);
        EnterBattle(clients[1], mapEventId);

        foreach (var instance in Clients.Append(Server))
            AssertHostController(instance, mapEventId, AllyControllerId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AssaultLeaderWithdrawsWhileLoading_WaitingAllyHosts(bool allyReadyFirst)
    {
        var mapEventId = SetupHideoutAssault();
        var clients = Clients.ToArray();

        EnterBattle(clients[0], mapEventId, missionReady: false);
        if (allyReadyFirst)
        {
            EnterBattle(clients[1], mapEventId);
            AssertNoHost(Server, mapEventId);
        }

        DepartBattle(LeaderControllerId, mapEventId, wasRetreat: true);
        if (!allyReadyFirst)
            EnterBattle(clients[1], mapEventId);

        foreach (var instance in Clients.Append(Server))
            AssertHostController(instance, mapEventId, AllyControllerId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AssaultLeaderDisconnectsWhileLoading_WaitingAllyHosts(bool secondAllyReadyBeforeDeparture)
    {
        var mapEventId = SetupHideoutAssault();
        var clients = Clients.ToArray();

        EnterBattle(clients[0], mapEventId, missionReady: false);
        EnterBattle(clients[1], mapEventId);
        AssertNoHost(Server, mapEventId);

        if (secondAllyReadyBeforeDeparture)
        {
            // The leader's peer clears at once while its map event departure still waits for the game thread
            Server.Call(() => Server.Resolve<IPlayerManager>().ClearPeer(clients[0].NetPeer));
            EnterBattle(clients[2], mapEventId);
        }
        else
        {
            Server.Call(() =>
            {
                Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(clients[0].NetPeer, default));
            });
            Server.PumpGameThread();
        }

        foreach (var instance in Clients.Append(Server))
            AssertHostController(instance, mapEventId, AllyControllerId);
    }

    [Fact]
    public void WaitingAlly_IsSentTheSingleAssignmentNamingTheLeader()
    {
        var mapEventId = SetupHideoutAssault();
        var clients = Clients.ToArray();

        EnterBattle(clients[1], mapEventId);
        Assert.Empty(HostAssignments(mapEventId));
        AssertNoHost(clients[1], mapEventId);

        EnterBattle(clients[0], mapEventId);

        var assignment = Assert.Single(HostAssignments(mapEventId));
        Assert.Equal(LeaderControllerId, assignment.HostControllerId);
        Assert.Equal(1, assignment.Epoch);
        AssertHostController(clients[1], mapEventId, LeaderControllerId);
        AssertIsLocalHost(clients[1], mapEventId, false);
    }

    [Fact]
    public void AssaultLeaderNeverGetsReady_WaitingAllyHostsAfterTheDeadline()
    {
        var mapEventId = SetupHideoutAssault();
        var clients = Clients.ToArray();

        EnterBattle(clients[0], mapEventId, missionReady: false);
        EnterBattle(clients[1], mapEventId);
        AssertNoHost(Server, mapEventId);

        // A slow leader load is still inside the default wait
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new CampaignTick()));
        AssertNoHost(Server, mapEventId);
        Server.Call(() => Assert.Equal(TimeSpan.FromSeconds(90), Server.Resolve<BattleHostHandler>().HideoutLeaderWaitDeadline));

        Server.Call(() => Server.Resolve<BattleHostHandler>().HideoutLeaderWaitDeadline = TimeSpan.Zero);
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new CampaignTick()));

        foreach (var instance in Clients.Append(Server))
            AssertHostController(instance, mapEventId, AllyControllerId);

        // The leader getting ready later joins the hosted battle instead of taking it over
        MakeMissionReady(clients[0], mapEventId);
        foreach (var instance in Clients.Append(Server))
            AssertHostController(instance, mapEventId, AllyControllerId);
        Assert.All(HostAssignments(mapEventId), message => Assert.Equal(AllyControllerId, message.HostControllerId));

        SendHideoutStateReady(clients[0], LeaderControllerId, mapEventId);
        AssertHost(Server, mapEventId, AllyControllerId, LeaderControllerId);
    }

    // CoopHideoutMissionLogic sends this again once the host's hideout state has reached the client
    private void SendHideoutStateReady(EnvironmentInstance client, string controllerId, string mapEventId) =>
        Server.SimulateMessage(client.NetPeer, new NetworkRequestBattleHost(mapEventId, controllerId, hideoutStateReady: true));

    private NetworkBattleHostAssigned[] HostAssignments(string mapEventId) =>
        Server.NetworkSentMessages.GetMessages<NetworkBattleHostAssigned>()
            .Where(message => message.MapEventId == mapEventId)
            .ToArray();

    // The leader's party starts the hideout battle, the ally joins the attacker side and the bandits defend
    private string SetupHideoutAssault(bool leaderConnected = true)
    {
        var clients = Clients.ToArray();
        SetControllerId(clients[0], LeaderControllerId);
        SetControllerId(clients[1], AllyControllerId);
        SetControllerId(clients[2], SecondAllyControllerId);

        string? mapEventId = null;
        string? leaderPartyId = null;
        string? allyPartyId = null;
        string? secondAllyPartyId = null;

        Server.Call(() =>
        {
            var leader = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var ally = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var secondAlly = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var bandits = GameObjectCreator.CreateInitializedObject<MobileParty>();

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();
            mapEvent.Initialize(leader.Party, bandits.Party);
            ally.Party.MapEventSide = mapEvent.AttackerSide;
            secondAlly.Party.MapEventSide = mapEvent.AttackerSide;
            mapEvent.MapEventVisual = null;
            Campaign.Current.MapEventManager.OnMapEventCreated(mapEvent);

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            Assert.True(Server.ObjectManager.TryGetId(leader, out leaderPartyId));
            Assert.True(Server.ObjectManager.TryGetId(ally, out allyPartyId));
            Assert.True(Server.ObjectManager.TryGetId(secondAlly, out secondAllyPartyId));
        }, MapEventDisabledMethods);

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out var mapEvent));
                mapEvent._mapEventType = MapEvent.BattleTypes.Hideout;
                Assert.True(mapEvent.IsHideoutBattle);
            });
        }

        RegisterAsPlayerParty(LeaderControllerId, CreateRegisteredObject<Hero>(), leaderPartyId!);
        RegisterAsPlayerParty(AllyControllerId, CreateRegisteredObject<Hero>(), allyPartyId!);
        RegisterAsPlayerParty(SecondAllyControllerId, CreateRegisteredObject<Hero>(), secondAllyPartyId!);
        Server.Call(() =>
        {
            var players = Server.Resolve<IPlayerManager>();
            if (leaderConnected)
                players.SetPeer(LeaderControllerId, clients[0].NetPeer);
            players.SetPeer(AllyControllerId, clients[1].NetPeer);
            players.SetPeer(SecondAllyControllerId, clients[2].NetPeer);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(leaderPartyId!, out var leader));
            Assert.Same(leader.Party, mapEvent.AttackerSide.LeaderParty);
        });

        return mapEventId!;
    }

    private static void AssertHostController(EnvironmentInstance instance, string mapEventId, string expectedHost)
    {
        instance.Call(() =>
        {
            Assert.True(instance.Resolve<IBattleHostRegistry>().TryGet(mapEventId, out var assignment));
            Assert.Equal(expectedHost, assignment.HostControllerId);
        });
    }
}
