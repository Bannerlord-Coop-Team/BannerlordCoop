using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Agents.Packets;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;
using AgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

/// <summary>A land battle host's process dies mid-battle and the server promotes its successor (#4861).</summary>
public class CrashedBattleHostTests : MissionTestEnvironment
{
    private readonly Guid ownTroopId = Guid.NewGuid();
    private readonly Guid npcTroopId = Guid.NewGuid();
    private readonly Dictionary<EnvironmentInstance, CoopBattleController> controllers = new();

    public CrashedBattleHostTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    // The crashed host's own troop and the enemy it ran sit frame-locked on the last pose it sent, on every other client. The
    // successor's AI has to move them again in the formation they held, and the observer keeps them as puppets.
    [Fact]
    public void CrashedHost_SuccessorFreesItsTroopsForTheAi_InTheirFormations()
    {
        using var engine = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("A", "npc", "B", "C");
        var clients = Clients.ToArray();
        SetControllerId(clients[1], "B");
        SetControllerId(clients[2], "C");
        var players = new[] { ("A", clients[0]), ("B", clients[1]), ("C", clients[2]) };
        var (a, b, c) = (clients[0], clients[1], clients[2]);
        string characterId = CreateRegisteredObject<CharacterObject>();

        try
        {
            foreach (var (_, client) in players)
                OpenMission(engine, client, mapEventId);
            foreach (var (id, _) in players)
                foreach (var (_, client) in players)
                    Publish(client, new NetworkMissionPeerEntered(id, mapEventId));
            foreach (var (_, client) in players)
                SpawnHostTroops(client, characterId, partyIds[0], partyIds[1]);
            SendHostMovement(a, ("B", b), ("C", c));

            // A's process dies: its mesh goes silent, then the server tells the others it disconnected and promotes B.
            a.Call(a.Resolve<MockBattleNetwork>().Stop);
            foreach (var survivor in new[] { b, c })
            {
                Publish(survivor, new MissionPeerDisconnected("A", mapEventId));
                Publish(survivor, new NetworkBattleHostAssigned(mapEventId, "B", new[] { "C" }, 2));
            }

            b.Call(() =>
            {
                AssertTroop(b, ownTroopId, "B", AgentControllerType.AI, FormationClass.Ranged);
                AssertTroop(b, npcTroopId, "B", AgentControllerType.AI, FormationClass.Cavalry);
                Assert.All(new[] { ownTroopId, npcTroopId }, id =>
                {
                    Assert.True(MockFormation.ForShell(Mirror(b, id).Formation, out var formation));
                    Assert.True(formation.IsAIControlled);
                    Assert.Equal(AgentMovementLockedState.None, Mirror(b, id).MovementLockedState);
                });
            });
            c.Call(() =>
            {
                AssertTroop(c, ownTroopId, "B", AgentControllerType.None, FormationClass.Ranged);
                AssertTroop(c, npcTroopId, "B", AgentControllerType.None, FormationClass.Cavalry);
            });
        }
        finally
        {
            foreach (var (_, client) in players)
                client.Call(BattleSpawnGate.EndBattle);
        }
    }

    private void Publish<T>(EnvironmentInstance client, T message) where T : IMessage =>
        client.Call(() => client.Resolve<IMessageBroker>().Publish(this, message));

    // An attacker player's mission with the side's ally team, as the coop launcher's teams stand, with A as the battle host.
    private void OpenMission(MissionEngineFixture engine, EnvironmentInstance client, string mapEventId) => client.Call(() =>
    {
        MockMission mission = CreateConnectedMission(engine, client, mapEventId);
        mission.PlayerTeam = mission.AttackerTeam;
        mission.AddTeam(BattleSideEnum.Attacker);
        mission.AddTeam(BattleSideEnum.Attacker);
        mission.AddTeam(BattleSideEnum.Defender);
        var controller = client.Resolve<CoopBattleController>();
        controller.Session.TryBegin(mapEventId);
        controllers[client] = controller;
        BattleSpawnGate.BeginBattle(mapEventId);
        client.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("A", new[] { "B", "C" }, 1));
    });

    // The records A broadcast for a troop of its own party and an enemy troop it ran, as every client applies them.
    private void SpawnHostTroops(EnvironmentInstance client, string characterId, string hostPartyId, string npcPartyId) =>
        client.Call(() =>
        {
            var records = new[]
            {
                Record(client, ownTroopId, characterId, BattleSideEnum.Attacker, hostPartyId, FormationClass.Ranged, 1),
                Record(client, npcTroopId, characterId, BattleSideEnum.Defender, npcPartyId, FormationClass.Cavalry, 2),
            };
            client.Resolve<IMessageBroker>().Publish(this, new NetworkSpawnBattleAgents(records));
        });

    private static BattleAgentSpawnData Record(EnvironmentInstance client, Guid agentId, string characterId,
        BattleSideEnum side, string partyId, FormationClass formation, ushort movementId)
    {
        Assert.True(client.ObjectManager.TryGetObject(partyId, out MobileParty party));
        var mapEventParty = party.Party.MapEventSide.Parties.Single(value => value.Party == party.Party);
        Assert.True(client.ObjectManager.TryGetId(mapEventParty, out var mapEventPartyId));
        return new BattleAgentSpawnData(agentId, characterId, Vec3.Zero, side, 100f, "A", mapEventPartyId, 0,
            new Equipment(), default, null, formationIndex: (int)formation, movementId: movementId, authorityRevision: 1);
    }

    // A reports both troops where they stand, and each observer's follow pins its puppets on that pose, as live.
    private void SendHostMovement(EnvironmentInstance host, params (string Id, EnvironmentInstance Client)[] observers)
    {
        var ids = new[] { ownTroopId, npcTroopId };
        MovementPacket packet = default;
        host.Call(() =>
        {
            var infos = ids.Select(id => Info(host, id)).ToArray();
            packet = new MovementPacket(ids, infos.Select(info => new AgentData(info.Agent)).ToArray(), "A",
                infos.Select(info => info.AuthorityRevision).ToArray());
        });
        foreach (var (id, observer) in observers)
        {
            host.Call(() => host.Resolve<MockBattleNetwork>().Send(id, packet));
            observer.Call(() =>
            {
                controllers[observer].AgentMovementHandler.Interpolator.Tick(1f / 60f);
                Assert.All(ids, agentId =>
                    Assert.Equal(AgentMovementLockedState.FrameLocked, Mirror(observer, agentId).MovementLockedState));
            });
        }
    }

    private static void AssertTroop(EnvironmentInstance client, Guid agentId, string authority,
        AgentControllerType controller, FormationClass formationClass)
    {
        Assert.Equal(authority, Info(client, agentId).CurrentAuthority);
        var mirror = Mirror(client, agentId);
        Assert.True(mirror.IsActive, $"{agentId} left the battle with its host");
        Assert.Equal(controller, mirror.Controller);
        Assert.True(MockFormation.ForShell(mirror.Formation, out var formation), $"{agentId} lost its formation");
        Assert.Equal(formationClass, formation.FormationClass);
    }

    private static CoopAgentInfo Info(EnvironmentInstance client, Guid agentId)
    {
        Assert.True(client.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(agentId, out var info), $"{agentId} is not registered");
        return info;
    }

    private static MirrorAgent Mirror(EnvironmentInstance client, Guid agentId)
    {
        Assert.True(AgentMirror.TryGet(Info(client, agentId).Agent, out var mirror));
        return mirror;
    }
}
