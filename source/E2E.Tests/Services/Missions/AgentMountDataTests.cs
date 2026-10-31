using Common.PacketHandlers;
using Common.Serialization;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Surrogates;
using Missions.Agents.Packets;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Services.Missions;

/// <summary>Pure <see cref="AgentMountData"/> turn/action classification and wire coverage; needs no game instances.</summary>
public class AgentMountDataTests
{
    public AgentMountDataTests()
    {
        _ = new SurrogateCollection();
    }

    [Fact]
    public void IsLocomotionAction_RecognizesWalkStrafeActionsWithCrossAnimations()
    {
        Assert.True(AgentMountData.IsLocomotionAction(
            "act_horse_forward_walk_strafe_right",
            "horse_strafe_r_cross_fast"));
    }

    [Theory]
    [InlineData("act_horse_turn_right", "", AgentMountData.TurnRight)]
    [InlineData("act_horse_turn_left", "", AgentMountData.TurnLeft)]
    [InlineData("", "rider_horse_rotate_right", AgentMountData.TurnRight)]
    [InlineData("", "rider_horse_rotate_left", AgentMountData.TurnLeft)]
    [InlineData("act_horse_forward_walk", "rider_forward_walk", AgentMountData.NoTurn)]
    public void GetTurnDirection_ClassifiesNativeStationaryTurnActions(
        string actionName,
        string animationName,
        int expected)
    {
        Assert.Equal(
            expected,
            AgentMountData.GetTurnDirection(actionName, animationName));
    }

    [Theory]
    [InlineData(0f, 1f, -1f, 0f, AgentMountData.TurnLeft)]
    [InlineData(0f, 1f, 1f, 0f, AgentMountData.TurnRight)]
    [InlineData(0f, 1f, 0.001f, 1f, AgentMountData.NoTurn)]
    [InlineData(0f, 1f, 0f, -1f, AgentMountData.TurnRight)]
    public void GetTurnDirection_DerivesStationaryTurnsFromFacingChanges(
        float previousX,
        float previousY,
        float currentX,
        float currentY,
        int expected)
    {
        Assert.Equal(
            expected,
            AgentMountData.GetTurnDirection(
                new Vec2(previousX, previousY),
                new Vec2(currentX, currentY)));
    }

    [Theory]
    [InlineData("", "horse", AgentMountData.TurnRight, "act_horse_turn_right")]
    [InlineData("", "horse", AgentMountData.TurnLeft, "act_horse_turn_left")]
    [InlineData("", "camel", AgentMountData.TurnRight, "act_camel_turn_right")]
    [InlineData("", "camel", AgentMountData.TurnLeft, "act_camel_turn_left")]
    [InlineData("act_camel_turn_right", "horse", AgentMountData.TurnRight, "act_camel_turn_right")]
    [InlineData("act_horse_walk_turn_right_head", "horse", AgentMountData.TurnRight, "act_horse_turn_right")]
    public void GetStationaryTurnActionName_UsesNativeMountActions(
        string authoritativeActionName,
        string monsterUsage,
        int direction,
        string expected)
    {
        Assert.Equal(
            expected,
            AgentMountData.GetStationaryTurnActionName(
                authoritativeActionName,
                monsterUsage,
                direction));
    }

    [Theory]
    [InlineData("act_horse_turn_right", true)]
    [InlineData("act_horse_turn_left", true)]
    [InlineData("act_camel_turn_right", true)]
    [InlineData("act_camel_turn_left", true)]
    [InlineData("act_horse_walk_turn_right_head", false)]
    [InlineData("act_camel_trot_turn_left_head", false)]
    [InlineData("act_horse_turn_right_head", false)]
    public void IsStationaryTurnAction_RejectsGaitAndHeadTurnActions(
        string actionName,
        bool expected)
    {
        Assert.Equal(
            expected,
            AgentMountData.IsStationaryTurnAction(actionName));
    }

    [Fact]
    public void ResolveAction0Index_SelectsTheMountSpecificStationaryTurnAction()
    {
        Assert.Equal(
            902,
            AgentMountData.ResolveAction0Index(
                actionIndex: 101,
                speed: 0f,
                isLocomotion: true,
                turnDirection: AgentMountData.TurnRight,
                turnActionIndex: 902));
    }

    [Fact]
    public void ResolveAction0Index_ClearsTheGaitAfterAStationaryTurnSettles()
    {
        Assert.Equal(
            AgentMountData.NoActionIndex,
            AgentMountData.ResolveAction0Index(
                actionIndex: 101,
                speed: 0f,
                isLocomotion: true,
                turnDirection: AgentMountData.NoTurn,
                turnActionIndex: 902));
    }

    [Fact]
    public void MountMovementPacket_RoundTripsHorizontalMountSpeed()
    {
        using var fixture = new MissionEngineFixture();
        Agent sourceHorse = new MockMission().SpawnMount();
        Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
        sourceHorseMirror.RealGlobalVelocity = new Vec3(3f, 4f, 12f);
        var horseId = Guid.NewGuid();
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());

        byte[] wire = serializer.Serialize(
            new MountMovementPacket(
                new[] { horseId },
                new[] { new AgentMountData(sourceHorse, horseId) }));
        var result = Assert.IsType<MountMovementPacket>(serializer.Deserialize<IPacket>(wire));

        Assert.Equal(5f, Assert.Single(result.Mounts).MountSpeed);
    }
}
