using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.Locations.Messages.Conversation;
using GameInterface.Services.Players;
using Missions;
using Missions.Taverns;
using Moq;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

/// <summary>
/// Composes campaign membership, host election, a headless mission per client, and the simulated mesh.
/// Scenario tests can build on this fixture without giving the dedicated server a mission or player agent.
/// </summary>
public class SettlementTestEnvironment : LocationHostTestEnvironment, IDisposable
{
    private readonly MissionEngineFixture missionEngine = new MissionEngineFixture();
    private readonly Dictionary<EnvironmentInstance, SettlementClientFixture> activeClients = new();
    private bool disposed;

    public SettlementTestEnvironment(ITestOutputHelper output, int numClients = 2) : base(output, numClients)
    {
    }

    public (string instanceId, string[] partyIds) CreateSettlement(params string[] controllerIds)
    {
        var settlement = SetupSettlementLocation(controllerIds);
        AttachLocationComplex(settlement.instanceId);
        return settlement;
    }

    // LocationComplex is not serialized by the headless registry, so rebuild its location link per instance.
    private void AttachLocationComplex(string instanceId)
    {
        string[] ids = instanceId.Split('|');

        void Attach(EnvironmentInstance instance)
        {
            instance.Call(() =>
            {
                Settlement settlement = instance.GetRegisteredObject<Settlement>(ids[0]);
                Location location = instance.GetRegisteredObject<Location>(ids[1]);
                var locationComplex = new LocationComplex();
                locationComplex._locations.Add(ids[1], location);
                location._ownerComplex = locationComplex;
                settlement.LocationComplex = locationComplex;
            });
        }

        Attach(Server);
        foreach (EnvironmentInstance client in Clients)
            Attach(client);
    }

    public (string heroId, string characterId) CreateHeroCharacter()
    {
        string heroId = CreateRegisteredObject<Hero>();
        string characterId = null;
        Server.Call(() =>
        {
            Hero hero = Server.GetRegisteredObject<Hero>(heroId);
            if (!Server.ObjectManager.TryGetId(hero.CharacterObject, out characterId))
                throw new InvalidOperationException("The hero character was not registered");
        });
        return (heroId, characterId);
    }

    public void EnterCampaignSettlement(string controllerId, string instanceId)
    {
        string settlementId = GetSettlementId(instanceId);
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            if (!playerManager.TryGetPlayer(controllerId, out var player))
                throw new InvalidOperationException($"Player {controllerId} is not registered");

            MobileParty party = Server.GetRegisteredObject<MobileParty>(player.MobilePartyId);
            Settlement settlement = Server.GetRegisteredObject<Settlement>(settlementId);
            EnsureHeadlessSettlementComponent(settlement);
            EnterSettlementAction.ApplyForParty(party, settlement);
        });
    }

    public void LeaveCampaignSettlement(string controllerId)
    {
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            if (!playerManager.TryGetPlayer(controllerId, out var player))
                throw new InvalidOperationException($"Player {controllerId} is not registered");

            MobileParty party = Server.GetRegisteredObject<MobileParty>(player.MobilePartyId);
            EnsureHeadlessSettlementComponent(party.CurrentSettlement);
            LeaveSettlementAction.ApplyForParty(party);
        });
    }

    public void AddAmbientLocationCharacter(string instanceId, string characterId)
    {
        if (string.IsNullOrEmpty(characterId))
            throw new ArgumentException("characterId is required", nameof(characterId));

        if (string.IsNullOrEmpty(instanceId))
            throw new ArgumentException("Invalid location instance id", nameof(instanceId));
        string[] ids = instanceId.Split('|');
        if (ids.Length != 2)
            throw new ArgumentException("Invalid location instance id", nameof(instanceId));

        // Campaign setup supplies the same roster to each process before native mission population runs.
        void Add(EnvironmentInstance instance)
        {
            instance.Call(() =>
            {
                Location location = instance.GetRegisteredObject<Location>(ids[1]);
                CharacterObject character = instance.GetRegisteredObject<CharacterObject>(characterId);
                LocationCharacter entry = LocationCharacterFactory.Create(
                    character,
                    originParty: null,
                    specialItem: null,
                    spawnTag: "npc_common",
                    actionSetCode: null,
                    behaviorsMethodName: null,
                    characterRelation: (int)LocationCharacter.CharacterRelations.Neutral,
                    fixedLocation: false,
                    useCivilianEquipment: true);
                location._characterList ??= new List<LocationCharacter>();
                location._characterList.Add(entry);
            });
        }

        Add(Server);
        foreach (EnvironmentInstance client in Clients)
            Add(client);
    }

    public SettlementClientFixture EnterLocation(
        EnvironmentInstance client,
        string instanceId,
        bool enableNativePopulationBoundary = false)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrEmpty(instanceId)) throw new ArgumentException("instanceId is required", nameof(instanceId));
        if (activeClients.ContainsKey(client))
            throw new InvalidOperationException("A client can only have one active settlement location");

        string[] ids = instanceId.Split('|');
        if (ids.Length != 2) throw new ArgumentException("Invalid location instance id", nameof(instanceId));

        string controllerId = client.Resolve<IControllerIdProvider>().ControllerId;
        if (string.IsNullOrEmpty(controllerId))
            throw new InvalidOperationException("The client has no controller id");

        ConnectRegisteredPlayer(client, controllerId);

        Settlement settlement = client.GetRegisteredObject<Settlement>(ids[0]);
        Location location = client.GetRegisteredObject<Location>(ids[1]);
        var campaignMission = new Mock<ICampaignMission>();
        campaignMission.SetupGet(value => value.Location).Returns(location);
        client.CampaignMissionContext = campaignMission.Object;

        MockMission mission = null;
        CoopLocationsController controller = null;
        Agent playerAgent = null;
        MockBattleNetwork mesh = null;

        try
        {
            client.Call(() =>
            {
                var playerManager = client.Resolve<IPlayerManager>();
                if (!playerManager.TryGetPlayer(controllerId, out var player))
                    throw new InvalidOperationException($"Player {controllerId} is not registered");

                CharacterObject character = client.GetRegisteredObject<CharacterObject>(player.CharacterObjectId);
                MobileParty playerParty = client.GetRegisteredObject<MobileParty>(player.MobilePartyId);
                mission = missionEngine.CreateMission(client);
                mission.PlayerTeam = mission.DefenderTeam;
                mission.MainParty = playerParty.Party;
                mission.LocationPopulationBoundaryEnabled = enableNativePopulationBoundary;
                if (enableNativePopulationBoundary)
                {
                    mission.NativeLocationPopulation = () =>
                    {
                        foreach (LocationCharacter entry in location.GetCharacterList())
                        {
                            Mission.Current.SpawnAgent(
                                entry.GetAgentBuildData()
                                    .Controller(AgentControllerType.AI)
                                    .Team(mission.DefenderTeam.Shell));
                        }
                    };
                }
                playerAgent = mission.SpawnAgent(
                    new AgentBuildData(character)
                        .Controller(AgentControllerType.Player)
                        .Team(mission.DefenderTeam.Shell)
                        .InitialPosition(Vec3.Zero));
                mission.MainAgent = playerAgent;

                controller = client.Resolve<CoopLocationsController>();
                mesh = client.Resolve<MockBattleNetwork>();
                client.Resolve<IMessageBroker>().Publish(
                    this,
                    new PlayerEnteredLocation(settlement, location));
                controller.OnRenderingStarted();
            });
        }
        catch
        {
            client.CampaignMissionContext = null;
            throw;
        }

        var fixture = new SettlementClientFixture(
            client,
            controllerId,
            instanceId,
            settlement,
            location,
            mission,
            controller,
            playerAgent,
            mesh);
        activeClients.Add(client, fixture);
        return fixture;
    }

    public void RunNativePopulation(SettlementClientFixture fixture)
    {
        if (fixture == null) throw new ArgumentNullException(nameof(fixture));
        fixture.Instance.Call(() => fixture.Mission.LocationAgentHandler.SpawnLocationCharacters());
    }

    public void LeaveLocation(EnvironmentInstance client)
    {
        if (!activeClients.TryGetValue(client, out var fixture)) return;
        fixture.Leave();
        activeClients.Remove(client);
    }

    public void Disconnect(EnvironmentInstance client)
    {
        if (activeClients.TryGetValue(client, out var fixture))
        {
            fixture.Disconnect();
            activeClients.Remove(client);
        }

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(
                this,
                new PlayerDisconnected(client.NetPeer, default));
            Server.Resolve<MockServer>().RemovePeer(client.NetPeer);
        });
    }

    public TimeSpan MeshLatency
    {
        get => Server.Resolve<IVirtualNetworkScheduler>().DefaultLatency;
        set => Server.Resolve<IVirtualNetworkScheduler>().DefaultLatency = value;
    }

    public void SetMeshLatency(
        SettlementClientFixture sender,
        SettlementClientFixture receiver,
        TimeSpan latency)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(receiver);
        Server.Resolve<IVirtualNetworkScheduler>().SetLatency(sender.Mesh, receiver.Mesh, latency);
    }

    public int AdvanceNetwork(TimeSpan elapsed)
    {
        return Server.Resolve<IVirtualNetworkScheduler>().AdvanceBy(elapsed);
    }

    public int DrainNetwork()
    {
        return Server.Resolve<IVirtualNetworkScheduler>().RunUntilIdle();
    }

    public void Tick(float elapsedSeconds)
    {
        foreach (var fixture in activeClients.Values.ToArray())
            fixture.Tick(elapsedSeconds);
    }

    public MirrorAgent GetAgentState(Agent agent)
    {
        if (!AgentMirror.TryGet(agent, out var state))
            throw new InvalidOperationException("The agent is not owned by the headless mission fixture");
        return state;
    }

    public CoopAgentInfo GetAgentInfo(SettlementClientFixture client, Agent agent)
    {
        ArgumentNullException.ThrowIfNull(client);
        CoopAgentInfo info = null;
        client.Instance.Call(() =>
        {
            if (!client.Instance.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(agent, out info))
                throw new InvalidOperationException("The agent is not registered in this mission");
        });
        return info;
    }

    public CoopAgentInfo GetAgentInfo(SettlementClientFixture client, Guid agentId)
    {
        ArgumentNullException.ThrowIfNull(client);
        CoopAgentInfo info = null;
        client.Instance.Call(() =>
        {
            if (!client.Instance.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(agentId, out info))
                throw new InvalidOperationException($"Agent {agentId} is not registered in this mission");
        });
        return info;
    }

    public void DespawnAgent(SettlementClientFixture owner, Agent agent)
    {
        ArgumentNullException.ThrowIfNull(owner);
        MirrorAgent state = GetAgentState(agent);
        owner.Instance.Call(() =>
        {
            state.IsActive = false;
            owner.Controller.OnAgentDeleted(agent);
            owner.Mission.DeleteAgent(agent);
        });
    }

    public void MoveAgent(Agent agent, Vec3 position, Vec2 direction)
    {
        MirrorAgent state = GetAgentState(agent);
        state.Position = position;
        state.MovementDirection = direction;
        state.LookDirection = new Vec3(direction.x, direction.y, 0f);
    }

    public void SetAgentAction(
        Agent agent,
        int actionIndex,
        float progress = 0f,
        float speed = 1f)
    {
        MirrorAgent state = GetAgentState(agent);
        state.Action0Index = actionIndex;
        state.Action0Progress = progress;
        state.Action0Speed = speed;
    }

    public Agent SpawnCompanion(
        SettlementClientFixture owner,
        string characterId,
        Vec3 position)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Agent companion = null;

        owner.Instance.Call(() =>
        {
            CharacterObject character = owner.Instance.GetRegisteredObject<CharacterObject>(characterId);
            if (character.HeroObject != null)
            {
                using (new Common.Util.AllowedThread())
                    character.HeroObject.PartyBelongedTo = owner.Mission.MainParty.MobileParty;
            }

            var origin = new PartyAgentOrigin(owner.Mission.MainParty, character);
            companion = Mission.Current.SpawnAgent(
                new AgentBuildData(character)
                    .Controller(AgentControllerType.AI)
                    .Team(owner.Mission.DefenderTeam.Shell)
                    .TroopOrigin(origin)
                    .InitialPosition(position));
            owner.Controller.OnAgentCreated(companion);
            owner.Controller.OnMissionTick(0f);
        });

        return companion;
    }

    public Agent SpawnNpc(
        SettlementClientFixture host,
        string characterId,
        Vec3 position,
        Vec2 direction)
    {
        ArgumentNullException.ThrowIfNull(host);
        Agent spawned = null;

        host.Instance.Call(() =>
        {
            CharacterObject character = host.Instance.GetRegisteredObject<CharacterObject>(characterId);
            spawned = Mission.Current.SpawnAgent(
                new AgentBuildData(character)
                    .Controller(AgentControllerType.AI)
                    .Team(host.Mission.DefenderTeam.Shell)
                    .InitialPosition(position)
                    .InitialDirection(direction));
            host.Controller.OnMissionTick(0f);
        });

        return spawned;
    }

    public void RequestDialogue(
        SettlementClientFixture initiator,
        string targetCharacterId,
        int generation)
    {
        ArgumentNullException.ThrowIfNull(initiator);

        initiator.Instance.Call(() =>
        {
            if (!initiator.Instance.ObjectManager.TryGetId(initiator.Location, out var locationId))
                throw new InvalidOperationException("The active location is not registered");
            initiator.Instance.Resolve<INetwork>().SendAll(
                new NetworkRequestLocationConversation(locationId, targetCharacterId, generation));
        });
    }

    public void EndDialogue(SettlementClientFixture participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        participant.Instance.Call(() =>
            participant.Instance.Resolve<INetwork>().SendAll(new NetworkLocationConversationEnded()));
    }

    public void ApplyAuthoritativeOutcome(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Server.Call(action);
    }

    private static void EnsureHeadlessSettlementComponent(Settlement? settlement)
    {
        if (settlement == null || settlement.SettlementComponent != null) return;
        settlement.SetSettlementComponent(new HeadlessSettlementComponent());
    }

    /// <summary>Supplies no-op settlement callbacks required by native campaign enter/leave actions.</summary>
    private sealed class HeadlessSettlementComponent : SettlementComponent
    {
        private readonly IFaction mapFaction = Mock.Of<IFaction>();

        public override IFaction MapFaction => mapFaction;
    }

    private static string GetSettlementId(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            throw new ArgumentException("instanceId is required", nameof(instanceId));

        int separator = instanceId.IndexOf('|');
        if (separator <= 0) throw new ArgumentException("Invalid location instance id", nameof(instanceId));
        return instanceId.Substring(0, separator);
    }

    public new void Dispose()
    {
        if (disposed) return;
        disposed = true;

        foreach (var fixture in activeClients.Values.ToArray())
            fixture.Disconnect();
        activeClients.Clear();

        missionEngine.Dispose();
        base.Dispose();
    }
}
