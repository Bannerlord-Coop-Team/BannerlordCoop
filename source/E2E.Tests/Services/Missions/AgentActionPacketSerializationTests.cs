using Common.PacketHandlers;
using Common.Serialization;
using Common.Util;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Surrogates;
using Missions.Agents.Packets;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class AgentActionPacketSerializationTests
{
    private readonly ProtoBufSerializer serializer = new ProtoBufSerializer(new SerializableTypeMapper());

    public AgentActionPacketSerializationTests()
    {
        _ = new SurrogateCollection();
    }

    [Fact]
    public void AgentActionPacket_RoundTripsBattleHostEpoch()
    {
        var original = new AgentActionPacket(
            "battle-host",
            Array.Empty<Guid>(),
            Array.Empty<AgentActionData>(),
            Array.Empty<long>(),
            battleHostEpoch: 7);

        byte[] wire = serializer.Serialize(original);
        var result = Assert.IsType<AgentActionPacket>(
            serializer.Deserialize<IPacket>(wire));

        Assert.Equal("battle-host", result.ControllerId);
        Assert.Equal(7, result.BattleHostEpoch);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(int.MaxValue)]
    public void AgentActionData_RoundTripsAction1Index(int action1Index)
    {
        using var fixture = new MissionEngineFixture();
        Agent owner = CreateAgent(out MirrorAgent mirror);
        mirror.Action1Index = action1Index;

        AgentActionData result = RoundTrip(new AgentActionData(owner));

        Assert.Equal(action1Index, result.Action1Index);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void AgentActionData_RoundTripsGuardChannels(int channel)
    {
        using var fixture = new MissionEngineFixture();
        Agent owner = CreateAgent(out _);
        // Only a mounted agent has a guard presentation channel.
        if (channel >= 0) new MockMission().SpawnMount(owner);

        AgentActionData result = RoundTrip(new AgentActionData(
            owner,
            Agent.MovementControlFlag.None,
            Agent.GuardMode.None,
            guardReactionChannel: channel));

        Assert.Equal(channel, result.GuardPresentationChannel);
        Assert.Equal(channel, result.GuardActionChannel);
    }

    [Fact]
    public void AgentActionData_WritesNoBytesForAnEmptyActionChannel()
    {
        using var fixture = new MissionEngineFixture();
        Agent idle = CreateAgent(out _);
        Agent acting = CreateAgent(out MirrorAgent actingMirror);
        actingMirror.Action1Index = 0;

        int idleBytes = serializer.Serialize(Packet(new AgentActionData(idle))).Length;
        int actingBytes = serializer.Serialize(Packet(new AgentActionData(acting))).Length;

        // act_none (-1) shifts to the omitted default; action 0 costs a tag and a one-byte value.
        Assert.Equal(actingBytes - 2, idleBytes);
    }

    [Theory]
    [InlineData(EquipmentIndex.None, EquipmentIndex.None)]
    [InlineData(EquipmentIndex.Weapon0, EquipmentIndex.None)]
    [InlineData(EquipmentIndex.ExtraWeaponSlot, EquipmentIndex.Weapon1)]
    public void AgentEquipmentData_RoundTripsWieldedHands(EquipmentIndex mainHand, EquipmentIndex offHand)
    {
        using var fixture = new MissionEngineFixture();
        Agent owner = CreateAgent(out _);
        var equipment = new AgentEquipmentData(mainHand, offHand, 0);

        AgentActionData result = RoundTrip(new AgentActionData(owner).WithEquipment(1, equipment));

        Assert.Equal((int)mainHand, result.Equipment.Value.MainHandIndex);
        Assert.Equal((int)offHand, result.Equipment.Value.OffHandIndex);
        Assert.Equal(equipment, result.Equipment.Value);
    }

    [Fact]
    public void AgentEquipmentData_EmptyArrowAmmoArrivesAsNull()
    {
        using var fixture = new MissionEngineFixture();
        Agent owner = CreateAgent(out _);
        var equipment = new AgentEquipmentData(
            EquipmentIndex.None,
            EquipmentIndex.None,
            0,
            arrowAmmo: Array.Empty<AgentArrowAmmoData>());

        AgentActionData result = RoundTrip(new AgentActionData(owner).WithEquipment(1, equipment));

        // protobuf-net writes nothing for an empty struct array, so readers must treat null as empty.
        Assert.Null(result.Equipment.Value.ArrowAmmo);
        Assert.Equal(equipment, result.Equipment.Value);
    }

    private AgentActionData RoundTrip(AgentActionData action)
    {
        byte[] wire = serializer.Serialize(Packet(action));
        var result = Assert.IsType<AgentActionPacket>(serializer.Deserialize<IPacket>(wire));
        return Assert.Single(result.Actions);
    }

    private static AgentActionPacket Packet(AgentActionData action) =>
        new AgentActionPacket("owner", new[] { Guid.NewGuid() }, new[] { action }, new[] { 1L });

    // Binds a skip-ctor agent to a mirror so the fixture shims serve its state.
    private static Agent CreateAgent(out MirrorAgent mirror)
    {
        var agent = ObjectHelper.SkipConstructor<Agent>();
        mirror = new MirrorAgent { Controller = AgentControllerType.None };
        AgentMirror.Bind(agent, mirror);
        return agent;
    }
}
