using Common.Util;
using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using Missions.Data;
using Missions.Messages;
using System.Reflection;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

public sealed class ClientReplicationResilienceTests
{
    [Fact]
    public void ReplicationValidator_AcceptsValidMovementWithoutAllocatingAfterWarmup()
    {
        var validator = new AgentReplicationValidator(2000, 1000);
        var packet = new MovementPacket(
            new[] { Guid.NewGuid() },
            new[] { AgentFrame() });
        for (int i = 0; i < 100; i++)
            Assert.True(validator.TryValidate(packet, out _));

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
        {
            if (!validator.TryValidate(packet, out _))
                throw new InvalidOperationException("valid movement was rejected");
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ReplicationValidator_RejectsDuplicateIdsAndInvalidNumbers()
    {
        var validator = new AgentReplicationValidator(4, 1000);
        Guid duplicate = Guid.NewGuid();
        var duplicates = new MovementPacket(
            new[] { duplicate, duplicate },
            new[] { AgentFrame(), AgentFrame() });
        Assert.False(validator.TryValidate(duplicates, out string duplicateFailure));
        Assert.Contains("duplicated", duplicateFailure);

        var nanPosition = new MovementPacket(
            new[] { Guid.NewGuid() },
            new[] { AgentFrame(position: new Vec3(float.NaN, 0f, 0f)) });
        Assert.False(validator.TryValidate(nanPosition, out string positionFailure));
        Assert.Contains("nonfinite", positionFailure);

        var infiniteSpeed = new MovementPacket(
            new[] { Guid.NewGuid() },
            new[] { AgentFrame(speed: float.PositiveInfinity) });
        Assert.False(validator.TryValidate(infiniteSpeed, out string speedFailure));
        Assert.Contains("nonfinite", speedFailure);

        var invalidMount = MountFrame();
        SetBackingField(
            invalidMount,
            nameof(AgentMountData.MountPosition),
            new Vec3(0f, float.PositiveInfinity, 0f));
        var mountPacket = new MountMovementPacket(
            new[] { Guid.NewGuid() },
            new[] { invalidMount });
        Assert.False(validator.TryValidate(mountPacket, out string mountFailure));
        Assert.Contains("nonfinite", mountFailure);
    }

    [Fact]
    public void ReplicationValidator_RejectsInvalidActionAndJoinStructure()
    {
        var validator = new AgentReplicationValidator(4, 1000);
        var action = ObjectHelper.SkipConstructor<AgentActionData>();
        SetBackingField(action, nameof(AgentActionData.MovementFlag), uint.MaxValue);
        var actionPacket = new AgentActionPacket(
            "controller",
            new[] { Guid.NewGuid() },
            new[] { action },
            new[] { 1L });
        Assert.False(validator.TryValidate(actionPacket, out string actionFailure));
        Assert.Contains("unsupported", actionFailure);

        var validAction = ObjectHelper.SkipConstructor<AgentActionData>();
        var unreasonableEpoch = new AgentActionPacket(
            "controller",
            new[] { Guid.NewGuid() },
            new[] { validAction },
            new[] { 1L },
            int.MaxValue);
        Assert.False(validator.TryValidate(unreasonableEpoch, out string epochFailure));
        Assert.Contains("unreasonable", epochFailure);

        var unreasonableSequence = new AgentActionPacket(
            "controller",
            new[] { Guid.NewGuid() },
            new[] { validAction },
            new[] { long.MaxValue });
        Assert.False(validator.TryValidate(unreasonableSequence, out string sequenceFailure));
        Assert.Contains("reasonable", sequenceFailure);

        Guid duplicate = Guid.NewGuid();
        var join = Join("controller", duplicate, duplicate);
        Assert.False(validator.TryValidate(join, out string joinFailure));
        Assert.Contains("duplicated", joinFailure);

        var invalidJoin = new NetworkMissionJoinInfo(
            "controller",
            isPlayerAlive: true,
            new[]
            {
                new CoopAgentSpawnData(
                    Guid.NewGuid(),
                    "npc_character",
                    Vec3.Zero,
                    float.NaN,
                    isPlayer: false),
            });
        Assert.False(validator.TryValidate(invalidJoin, out string healthFailure));
        Assert.Contains("nonfinite", healthFailure);
    }

    [Fact]
    public void ReplicationValidator_AcceptsKnownNativeFlagsAndRejectsUnknownActionIndices()
    {
        var validator = new AgentReplicationValidator(4, 1000);
        var validFlags = ObjectHelper.SkipConstructor<AgentActionData>();
        SetBackingField(
            validFlags,
            nameof(AgentActionData.Action0Flag),
            (ulong)(AnimFlags.anf_restart | AnimFlags.anf_synch_with_movement));
        var validPacket = new AgentActionPacket(
            "controller",
            new[] { Guid.NewGuid() },
            new[] { validFlags },
            new[] { 1L });
        Assert.True(validator.TryValidate(validPacket, out _));

        var unknownAction = ObjectHelper.SkipConstructor<AgentActionData>();
        SetBackingField(unknownAction, nameof(AgentActionData.Action0Index), 1000);
        var unknownPacket = new AgentActionPacket(
            "controller",
            new[] { Guid.NewGuid() },
            new[] { unknownAction },
            new[] { 1L });
        Assert.False(validator.TryValidate(unknownPacket, out string failure));
        Assert.Contains("action index", failure);
    }

    [Fact]
    public void ReplicationValidator_RejectsAmbiguousMountIdentities()
    {
        var validator = new AgentReplicationValidator(4, 1000);
        AgentMountData bothIds = MountFrame();
        SetBackingField(bothIds, nameof(AgentMountData.MountMovementId), (ushort)5);
        SetBackingField(bothIds, nameof(AgentMountData.MountAgentId), Guid.NewGuid());
        var bothIdsPacket = new MountMovementPacket(
            new[] { Guid.NewGuid() },
            new[] { bothIds });
        Assert.False(validator.TryValidate(bothIdsPacket, out string bothIdsFailure));
        Assert.Contains("cannot both", bothIdsFailure);

        AgentMountData scopeWithoutCompactId = MountFrame();
        SetBackingField(
            scopeWithoutCompactId,
            nameof(AgentMountData.MountIdentityScopeId),
            "other-controller");
        var scopePacket = new MountMovementPacket(
            new[] { Guid.NewGuid() },
            new[] { scopeWithoutCompactId });
        Assert.False(validator.TryValidate(scopePacket, out string scopeFailure));
        Assert.Contains("requires a compact id", scopeFailure);
    }

    [Fact]
    public void ReplicationValidator_RateLimitsApplicationFailureLogs()
    {
        var validator = new AgentReplicationValidator(4, 1000);

        Assert.True(validator.ShouldLogApplicationFailure(out int firstSuppressed));
        Assert.Equal(0, firstSuppressed);
        Assert.False(validator.ShouldLogApplicationFailure(out int secondSuppressed));
        Assert.Equal(0, secondSuppressed);
    }

    private static NetworkMissionJoinInfo Join(string controllerId, params Guid[] ids) =>
        new NetworkMissionJoinInfo(
            controllerId,
            isPlayerAlive: true,
            ids.Select(id => new CoopAgentSpawnData(
                id,
                "npc_character",
                Vec3.Zero,
                health: 100f,
                isPlayer: false)).ToArray());

    private static AgentData AgentFrame(Vec3? position = null, float speed = 0f)
    {
        object boxed = default(AgentData);
        SetBackingField(boxed, nameof(AgentData.Position), position ?? Vec3.Zero);
        SetBackingField(boxed, nameof(AgentData.LookDirection), new Vec3(0f, 1f, 0f));
        SetBackingField(boxed, nameof(AgentData.MovementDirection), Vec2.Zero);
        SetBackingField(boxed, nameof(AgentData.InputVector), Vec2.Zero);
        SetBackingField(boxed, nameof(AgentData.Speed), speed);
        return (AgentData)boxed;
    }

    private static AgentMountData MountFrame()
    {
        var data = ObjectHelper.SkipConstructor<AgentMountData>();
        SetBackingField(data, nameof(AgentMountData.MountPosition), Vec3.Zero);
        SetBackingField(data, nameof(AgentMountData.MountLookDirection), new Vec3(0f, 1f, 0f));
        SetBackingField(data, nameof(AgentMountData.MountMovementDirection), Vec2.Zero);
        SetBackingField(data, nameof(AgentMountData.MountInputVector), Vec2.Zero);
        SetBackingField(data, nameof(AgentMountData.MountAction0Speed), 1f);
        return data;
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        FieldInfo field = instance.GetType().GetField(
            $"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(instance.GetType().FullName, propertyName);
        field.SetValue(instance, value);
    }
}
