using Missions.Agents.Packets;
using Missions.Messages;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

/// <summary>Checks settlement NPC and companion replication through the headless mission mesh.</summary>
public class SettlementAgentReplicationTests : SettlementTestEnvironment
{
    public SettlementAgentReplicationTests(ITestOutputHelper output) : base(output, numClients: 2)
    {
    }

    [Fact]
    public void SpawnNpc_ReplicatesIdentityTransformMovementAndActionOnceAtConfiguredLatency()
    {
        var clients = Clients.ToArray();
        var (instanceId, _) = CreateSettlement("A", "B");
        SettlementClientFixture owner = EnterLocation(clients[0], instanceId);
        SettlementClientFixture receiver = EnterLocation(clients[1], instanceId);
        DrainNetwork();
        string characterId = CreateRegisteredObject<TaleWorlds.CampaignSystem.CharacterObject>();
        var spawnPosition = new Vec3(12f, 4f, 0f);
        var spawnDirection = new Vec2(0.6f, 0.8f);
        SetMeshLatency(owner, receiver, TimeSpan.FromMilliseconds(100));

        Agent ownedNpc = SpawnNpc(owner, characterId, spawnPosition, spawnDirection);

        Assert.Equal(2, receiver.Mission.Agents.Count);
        Assert.Equal(0, AdvanceNetwork(TimeSpan.FromMilliseconds(99)));
        Assert.Equal(2, receiver.Mission.Agents.Count);
        Assert.True(AdvanceNetwork(TimeSpan.FromMilliseconds(1)) > 0);

        var ownedInfo = GetAgentInfo(owner, ownedNpc);
        var receivedInfo = GetAgentInfo(receiver, ownedInfo.AgentId);
        Agent receivedNpc = receivedInfo.Agent;
        var ownedState = GetAgentState(ownedNpc);
        var receivedState = GetAgentState(receivedNpc);

        Assert.NotEqual(Guid.Empty, ownedInfo.AgentId);
        Assert.Equal(ownedInfo.AgentId, receivedInfo.AgentId);
        Assert.Equal(ownedInfo.MovementId, receivedInfo.MovementId);
        Assert.Equal(ownedInfo.MovementScopeId, receivedInfo.MovementScopeId);
        Assert.Equal(owner.ControllerId, ownedInfo.CurrentAuthority);
        Assert.Equal(owner.ControllerId, receivedInfo.CurrentAuthority);
        Assert.Equal(owner.ControllerId, receivedInfo.OriginalOwner);
        Assert.Same(owner.Instance.GetRegisteredObject<TaleWorlds.CampaignSystem.CharacterObject>(characterId), ownedState.Character);
        Assert.Same(receiver.Instance.GetRegisteredObject<TaleWorlds.CampaignSystem.CharacterObject>(characterId), receivedState.Character);
        Assert.Equal(AgentControllerType.AI, ownedState.Controller);
        Assert.Equal(AgentControllerType.None, receivedState.Controller);
        Assert.Equal(spawnPosition, receivedState.Position);
        Assert.Equal(spawnDirection, receivedState.MovementDirection);
        Assert.Equal(3, owner.Mission.Agents.Count);
        Assert.Equal(3, receiver.Mission.Agents.Count);
        Assert.Empty(receiver.Mesh.NetworkSentMessages.OfType<NetworkSpawnLocationAgents>());

        var movedPosition = new Vec3(18f, 9f, 0f);
        var movedDirection = new Vec2(-0.8f, 0.6f);
        MoveAgent(ownedNpc, movedPosition, movedDirection);
        ownedState.RealGlobalVelocity = new Vec3(-3.2f, 2.4f, 0f);
        ownedState.InputVector = Vec2.Forward;
        ownedState.MovementFlags = Agent.MovementControlFlag.Forward;
        int movementWritesBeforeDelivery = receivedState.SetMovementDirectionCalls;

        Tick(0.05f);

        Assert.Equal(movementWritesBeforeDelivery, receivedState.SetMovementDirectionCalls);
        Assert.Equal(0, AdvanceNetwork(TimeSpan.FromMilliseconds(99)));
        Assert.Equal(movementWritesBeforeDelivery, receivedState.SetMovementDirectionCalls);
        Assert.True(AdvanceNetwork(TimeSpan.FromMilliseconds(1)) > 0);
        receiver.Tick(0.05f);

        Assert.Equal(movedDirection, receivedState.MovementDirection);
        Assert.Equal(new Vec3(movedDirection.x, movedDirection.y, 0f), receivedState.LookDirection);
        Assert.Equal(Agent.MovementControlFlag.Forward, receivedState.MovementFlags & Agent.MovementControlFlag.MoveMask);
        Assert.True(receivedState.SetMovementDirectionCalls > movementWritesBeforeDelivery);
        Assert.True(receivedState.SetMovementInputCalls > 0);
        Assert.True(receivedState.SetTargetPositionAndDirectionCalls > 0);
        Assert.Equal(movedPosition.AsVec2, receivedState.LastTargetPosition);
        Assert.Equal(new Vec3(movedDirection.x, movedDirection.y, 0f), receivedState.LastTargetDirection);

        const int actionIndex = 231;
        const float actionProgress = 0.37f;
        const float actionSpeed = 1.4f;
        SetAgentAction(ownedNpc, actionIndex, actionProgress, actionSpeed);

        Tick(0.05f);

        AgentActionPacket outboundAction = Assert.Single(
            owner.Mesh.NetworkSentPackets
                .GetPackets<AgentActionPacket>()
                .Where(packet =>
                {
                    int offset = Array.IndexOf(packet.AgentIds, ownedInfo.AgentId);
                    return offset >= 0 && packet.Actions[offset].Action0Index == actionIndex;
                }));
        int actionOffset = Array.IndexOf(outboundAction.AgentIds, ownedInfo.AgentId);
        Assert.Equal(actionIndex, outboundAction.Actions[actionOffset].Action0Index);
        Assert.Equal(actionProgress, outboundAction.Actions[actionOffset].Action0Progress);
        Assert.Equal(actionSpeed, outboundAction.Actions[actionOffset].Action0Speed);
        Assert.NotEqual(actionIndex, receivedState.Action0Index);
        Assert.Equal(0, AdvanceNetwork(TimeSpan.FromMilliseconds(99)));
        Assert.NotEqual(actionIndex, receivedState.Action0Index);
        Assert.True(AdvanceNetwork(TimeSpan.FromMilliseconds(1)) > 0);

        Assert.Equal(actionIndex, receivedState.Action0Index);
        Assert.Equal(actionProgress, receivedState.Action0Progress);
        Assert.Equal(actionSpeed, receivedState.Action0Speed);

        const float updatedActionSpeed = 0.65f;
        SetAgentAction(ownedNpc, actionIndex, actionProgress, updatedActionSpeed);
        int actionTransitionsBeforeSpeedApply = receivedState.SetActionChannelCalls;
        int speedWritesBeforeSpeedApply = receivedState.SetCurrentActionSpeedCalls;

        Tick(0.05f);

        Assert.Single(
            owner.Mesh.NetworkSentPackets
                .GetPackets<AgentActionPacket>()
                .Where(packet =>
                {
                    int offset = Array.IndexOf(packet.AgentIds, ownedInfo.AgentId);
                    return offset >= 0
                        && packet.Actions[offset].Action0Index == actionIndex
                        && packet.Actions[offset].Action0Speed == updatedActionSpeed;
                }));
        Assert.Equal(actionSpeed, receivedState.Action0Speed);
        Assert.Equal(0, AdvanceNetwork(TimeSpan.FromMilliseconds(99)));
        Assert.Equal(actionSpeed, receivedState.Action0Speed);
        Assert.True(AdvanceNetwork(TimeSpan.FromMilliseconds(1)) > 0);

        Assert.Equal(actionIndex, receivedState.Action0Index);
        Assert.Equal(updatedActionSpeed, receivedState.Action0Speed);
        Assert.Equal(actionTransitionsBeforeSpeedApply, receivedState.SetActionChannelCalls);
        Assert.True(receivedState.SetCurrentActionSpeedCalls > speedWritesBeforeSpeedApply);
        Assert.Equal(3, owner.Mission.Agents.Count);
        Assert.Equal(3, receiver.Mission.Agents.Count);
        Assert.Empty(receiver.Mesh.NetworkSentMessages.OfType<NetworkSpawnLocationAgents>());

        DespawnAgent(owner, ownedNpc);
        owner.Tick(0f);

        Assert.True(receivedState.IsActive);
        AdvanceNetwork(TimeSpan.FromMilliseconds(99));
        Assert.True(receivedState.IsActive);
        Assert.True(AdvanceNetwork(TimeSpan.FromMilliseconds(1)) > 0);
        owner.Instance.Call(() =>
            Assert.False(owner.Instance.Resolve<global::Missions.INetworkAgentRegistry>()
                .TryGetAgentInfo(ownedInfo.AgentId, out _)));
        receiver.Instance.Call(() =>
            Assert.False(receiver.Instance.Resolve<global::Missions.INetworkAgentRegistry>()
                .TryGetAgentInfo(ownedInfo.AgentId, out _)));
        Assert.False(receivedState.IsActive);
        Assert.Equal(2, owner.Mission.Agents.Count);
        Assert.Equal(3, receiver.Mission.Agents.Count);
    }

    [Fact]
    public void AmbientSpeedJitter_ManyNpcsKeepsReliableActionOutputBounded()
    {
        const int npcCount = 24;
        const int jitterFrames = 120;
        const int actionIndex = 231;
        var clients = Clients.ToArray();
        var (instanceId, _) = CreateSettlement("A", "B");
        SettlementClientFixture owner = EnterLocation(clients[0], instanceId);
        EnterLocation(clients[1], instanceId);
        DrainNetwork();
        string characterId =
            CreateRegisteredObject<TaleWorlds.CampaignSystem.CharacterObject>();
        Agent[] npcs = Enumerable.Range(0, npcCount)
            .Select(index => SpawnNpc(
                owner,
                characterId,
                new Vec3(index, 0f, 0f),
                Vec2.Forward))
            .ToArray();
        DrainNetwork();

        foreach (Agent npc in npcs)
            SetAgentAction(npc, actionIndex, speed: 1f);
        Tick(0.05f);
        DrainNetwork();

        var states = npcs.Select(GetAgentState).ToArray();
        foreach (var state in states)
            state.GetCurrentActionSpeedCalls = 0;
        owner.Mesh.NetworkSentPackets.Packets.Clear();

        for (int frame = 0; frame < jitterFrames; frame++)
        {
            for (int index = 0; index < states.Length; index++)
            {
                int jitterStep = ((frame + index) % 5) - 2;
                states[index].Action0Speed = 1f + (jitterStep * 0.005f);
            }

            Tick(1f / 60f);
        }

        Assert.Empty(
            owner.Mesh.NetworkSentPackets
                .GetPackets<AgentActionPacket>());
        Assert.Equal(
            npcCount * jitterFrames,
            states.Sum(state => state.GetCurrentActionSpeedCalls));

        foreach (var state in states)
            state.Action0Speed = 0.65f;
        Tick(1f / 60f);

        AgentActionPacket[] significantUpdates = owner.Mesh.NetworkSentPackets
            .GetPackets<AgentActionPacket>()
            .ToArray();
        Assert.Equal(3, significantUpdates.Length);
        Assert.Equal(
            npcCount,
            significantUpdates.Sum(packet => packet.AgentIds.Length));
        Assert.All(
            significantUpdates.SelectMany(packet => packet.Actions),
            action => Assert.Equal(0.65f, action.Action0Speed));
    }

    [Fact]
    public void SpawnCompanion_ReplicatesCharacterGuidOriginAuthorityAndDespawn()
    {
        var clients = Clients.ToArray();
        var (instanceId, _) = CreateSettlement("A", "B");
        SettlementClientFixture owner = EnterLocation(clients[0], instanceId);
        SettlementClientFixture receiver = EnterLocation(clients[1], instanceId);
        DrainNetwork();
        var (_, characterId) = CreateHeroCharacter();
        var spawnPosition = new Vec3(7f, -3f, 0f);

        Agent ownedCompanion = SpawnCompanion(owner, characterId, spawnPosition);
        DrainNetwork();

        var ownedInfo = GetAgentInfo(owner, ownedCompanion);
        var receivedInfo = GetAgentInfo(receiver, ownedInfo.AgentId);
        Agent receivedCompanion = receivedInfo.Agent;
        var ownedState = GetAgentState(ownedCompanion);
        var receivedState = GetAgentState(receivedCompanion);
        var ownedOrigin = Assert.IsType<PartyAgentOrigin>(ownedState.Origin);
        var receivedOrigin = Assert.IsType<SimpleAgentOrigin>(receivedState.Origin);

        Assert.NotEqual(Guid.Empty, ownedInfo.AgentId);
        Assert.Equal(ownedInfo.AgentId, receivedInfo.AgentId);
        Assert.Equal(owner.ControllerId, ownedInfo.CurrentAuthority);
        Assert.Equal(owner.ControllerId, receivedInfo.CurrentAuthority);
        Assert.Equal(owner.ControllerId, receivedInfo.OriginalOwner);
        Assert.Same(owner.Mission.MainParty, ownedOrigin.Party);
        Assert.Same(owner.Instance.GetRegisteredObject<TaleWorlds.CampaignSystem.CharacterObject>(characterId), ownedOrigin.Troop);
        Assert.Same(receiver.Instance.GetRegisteredObject<TaleWorlds.CampaignSystem.CharacterObject>(characterId), receivedOrigin.Troop);
        Assert.Same(ownedOrigin.Troop, ownedState.Character);
        Assert.Same(receivedOrigin.Troop, receivedState.Character);
        Assert.Equal(AgentControllerType.AI, ownedState.Controller);
        Assert.Equal(AgentControllerType.None, receivedState.Controller);
        Assert.Equal(spawnPosition, receivedState.Position);
        Assert.Equal(3, owner.Mission.Agents.Count);
        Assert.Equal(3, receiver.Mission.Agents.Count);
        Assert.Empty(receiver.Mesh.NetworkSentMessages.OfType<NetworkSpawnLocationAgents>());

        DespawnAgent(owner, ownedCompanion);
        DrainNetwork();

        owner.Instance.Call(() =>
            Assert.False(owner.Instance.Resolve<global::Missions.INetworkAgentRegistry>()
                .TryGetAgentInfo(ownedInfo.AgentId, out _)));
        receiver.Instance.Call(() =>
            Assert.False(receiver.Instance.Resolve<global::Missions.INetworkAgentRegistry>()
                .TryGetAgentInfo(ownedInfo.AgentId, out _)));
        Assert.False(receivedState.IsActive);
        Assert.Equal(2, owner.Mission.Agents.Count);
        Assert.Equal(3, receiver.Mission.Agents.Count);
    }
}
