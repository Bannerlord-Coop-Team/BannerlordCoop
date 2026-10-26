using Missions.Agents.Packets;
using Missions.Messages;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>The battle spawn record's copy rules.</summary>
public class BattleAgentSpawnDataTests
{
    // R50 F3: the initial flush moves a record to its agent's current spot, so every other field must survive the copy.
    [Fact]
    public void WithPosition_MovesTheRecordAndKeepsEveryOtherField()
    {
        var record = new BattleAgentSpawnData(Guid.NewGuid(), "character", new Vec3(1f, 2f, 3f), BattleSideEnum.Attacker, 42f,
            "owner", "party", 7, null, default, null, Guid.NewGuid(), 3, 11, 12, "original", default(AgentEquipmentData), "scope",
            "mountOriginal", "mountScope", isRunningAway: true, authorityRevision: 5, mountAuthorityRevision: 6,
            mountOwnerControllerId: "mountOwner", siegeEquipmentGrant: Guid.NewGuid(), siegeEquipmentGrantRevision: 8,
            shipId: Guid.NewGuid());
        var seat = new Vec3(40f, 7f, 3f);

        var moved = record.WithPosition(seat);

        Assert.Equal(seat, moved.Position);
        var kept = typeof(BattleAgentSpawnData).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(field => field.Name != nameof(BattleAgentSpawnData.Position));
        Assert.All(kept, field => Assert.Equal(field.GetValue(record), field.GetValue(moved)));
    }
}
