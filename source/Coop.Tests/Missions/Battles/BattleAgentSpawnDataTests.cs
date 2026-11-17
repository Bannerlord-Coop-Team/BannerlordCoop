using Missions.Messages;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>The battle spawn record's copy rules.</summary>
public class BattleAgentSpawnDataTests
{
    // R50 F3: the initial flush moves a record to its agent's current spot without touching the captured record.
    [Fact]
    public void WithPosition_MovesACopyAndLeavesTheRecord()
    {
        var spawn = new Vec3(1f, 2f, 3f);
        var record = new BattleAgentSpawnData(Guid.NewGuid(), "character", spawn, BattleSideEnum.Attacker, 42f, "owner",
            "party", 7, null, default, null, shipId: Guid.NewGuid());
        var seat = new Vec3(40f, 7f, 3f);

        var moved = record.WithPosition(seat);

        Assert.Equal(seat, moved.Position);
        Assert.Equal(spawn, record.Position);
        Assert.Equal(record.ShipId, moved.ShipId);
    }
}
