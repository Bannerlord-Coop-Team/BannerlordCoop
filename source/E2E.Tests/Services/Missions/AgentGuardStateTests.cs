using Common.PacketHandlers;
using Common.Serialization;
using Common.Util;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Surrogates;
using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Services.Missions;

/// <summary>Exhaustive guard-direction mapping for <see cref="AgentActionData"/> on mock agents; needs no game instances.</summary>
public class AgentGuardStateTests
{
    public AgentGuardStateTests()
    {
        _ = new SurrogateCollection();
    }

    [Theory]
    [InlineData(Agent.GuardMode.Up)]
    [InlineData(Agent.GuardMode.Down)]
    [InlineData(Agent.GuardMode.Left)]
    [InlineData(Agent.GuardMode.Right)]
    public void GuardApply_MapsEveryGuardDirection(Agent.GuardMode guardMode)
    {
        using var fixture = new MissionEngineFixture();
        Agent puppet = CreateAgent(AgentControllerType.None, out MirrorAgent mirror);

        AgentActionData.ApplyGuardState(puppet, guardMode);

        Assert.Equal(guardMode, mirror.GuardMode);
        Assert.Equal(1, mirror.SetWeaponGuardCalls);
        Assert.Equal(0, mirror.ResetGuardCalls);
    }

    [Fact]
    public void GuardApply_SkipsRedundantGuardCommandsAndReset()
    {
        using var fixture = new MissionEngineFixture();
        Agent puppet = CreateAgent(AgentControllerType.None, out MirrorAgent mirror);

        AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.None);
        Assert.Equal(0, mirror.ResetGuardCalls);

        AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.Up);
        AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.Up);
        Assert.Equal(1, mirror.SetWeaponGuardCalls);

        AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.None);
        AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.None);
        Assert.Equal(Agent.GuardMode.None, mirror.GuardMode);
        Assert.Equal(1, mirror.ResetGuardCalls);
    }

    [Theory]
    [InlineData(Agent.MovementControlFlag.DefendUp, Agent.GuardMode.Up)]
    [InlineData(Agent.MovementControlFlag.DefendDown, Agent.GuardMode.Down)]
    [InlineData(Agent.MovementControlFlag.DefendLeft, Agent.GuardMode.Left)]
    [InlineData(Agent.MovementControlFlag.DefendRight, Agent.GuardMode.Right)]
    public void EffectiveGuard_MapsEveryDefendFlagDirection(
        Agent.MovementControlFlag defendDirection,
        Agent.GuardMode expectedGuardMode)
    {
        using var fixture = new MissionEngineFixture();
        Agent owner = CreateAgent(AgentControllerType.Player, out MirrorAgent ownerMirror);
        ownerMirror.MovementFlags = Agent.MovementControlFlag.DefendBlock | defendDirection;

        Assert.Equal(expectedGuardMode, new AgentActionData(owner).GuardMode);
    }

    [Theory]
    [InlineData(Agent.UsageDirection.DefendUp, Agent.GuardMode.Up)]
    [InlineData(Agent.UsageDirection.DefendDown, Agent.GuardMode.Down)]
    [InlineData(Agent.UsageDirection.DefendLeft, Agent.GuardMode.Left)]
    [InlineData(Agent.UsageDirection.DefendRight, Agent.GuardMode.Right)]
    [InlineData(Agent.UsageDirection.None, Agent.GuardMode.None)]
    [InlineData(Agent.UsageDirection.AttackUp, Agent.GuardMode.None)]
    [InlineData(Agent.UsageDirection.AttackRight, Agent.GuardMode.None)]
    [InlineData(Agent.UsageDirection.DefendAny, Agent.GuardMode.None)]
    public void EffectiveGuard_MapsOnlyDirectionalDefendActions(
        Agent.UsageDirection actionDirection,
        Agent.GuardMode expectedGuardMode)
    {
        using var fixture = new MissionEngineFixture();
        Agent owner = CreateAgent(AgentControllerType.Player, out MirrorAgent ownerMirror);
        ownerMirror.MovementFlags = Agent.MovementControlFlag.DefendBlock;
        ownerMirror.Action1Direction = actionDirection;

        Assert.Equal(expectedGuardMode, new AgentActionData(owner).GuardMode);
    }

    [Fact]
    public void AgentActionPacket_RoundTripsEffectiveMountedGuard()
    {
        using var fixture = new MissionEngineFixture();
        var agentId = Guid.NewGuid();
        Agent owner = CreateAgent(AgentControllerType.Player, out MirrorAgent ownerMirror);
        new MockMission().SpawnMount(owner);
        ownerMirror.MovementFlags = Agent.MovementControlFlag.DefendBlock;
        ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
        ownerMirror.Action1Direction = Agent.UsageDirection.DefendLeft;

        var original = new AgentActionPacket(
            "owner",
            new[] { agentId },
            new[] { new AgentActionData(owner) },
            new[] { 1L });
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());

        byte[] wire = serializer.Serialize(original);
        var result = Assert.IsType<AgentActionPacket>(
            serializer.Deserialize<IPacket>(wire));

        AgentActionData action = Assert.Single(result.Actions);
        Assert.Equal(
            Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendLeft,
            action.DefendFlags);
        Assert.Equal(Agent.GuardMode.Left, action.GuardMode);
        Assert.Equal(1, action.GuardPresentationChannel);
        Assert.Equal(1, action.GuardActionChannel);
        Assert.True(action.GuardActionIsDefending);
        Assert.True(action.IsMounted);
    }

    [Theory]
    [InlineData(
        "act_defend_up_1h_passive",
        "act_defend_up_1h_parry_light")]
    [InlineData(
        "act_defend_right_2h_active_left_stance",
        "act_defend_right_2h_parry_light_left_stance")]
    [InlineData(
        "act_defend_shield_left_1h_passive_down",
        "act_defend_shield_left_1h_parry_light_down")]
    [InlineData(null, null)]
    [InlineData("act_strike_left_1h", null)]
    public void GuardReactionActionResolver_MapsHeldGuardToLightParry(
        string? guardActionName,
        string? expected)
    {
        Assert.Equal(
            expected,
            GuardReactionActionResolver
                .GetParryLightActionName(guardActionName));
    }

    // Binds a skip-ctor agent to a mirror so the fixture shims serve its state.
    private static Agent CreateAgent(AgentControllerType controller, out MirrorAgent mirror)
    {
        var agent = ObjectHelper.SkipConstructor<Agent>();
        mirror = new MirrorAgent { Controller = controller };
        AgentMirror.Bind(agent, mirror);
        return agent;
    }
}
