#if COOP_NAVAL && DEBUG
using Common.Commands;
using HarmonyLib;
using Missions.Naval;
using Moq;
using NavalDLC.Missions.Objects;
using Newtonsoft.Json.Linq;
using System;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks bounded network-id evidence and diagnostic patch binding without starting a mission.</summary>
[Collection("Mission.Current")]
public class NavalRamProbeTests
{
    /// <summary>Repeated broadcasts are retained in order, capped, and reset with the mission counters.</summary>
    [Fact]
    public void DeathIds_KeepRecentNetworkIdsAndClearAtMissionEnd()
    {
        NavalContactProbe.Clear();
        try
        {
            var first = Guid.NewGuid();
            var last = Guid.NewGuid();
            NavalContactProbe.RecordDeathBroadcast(first);
            for (int index = 0; index < 128; index++) NavalContactProbe.RecordDeathBroadcast(last);
            NavalContactProbe.RecordReceivedDeath(last);
            var state = JObject.FromObject(NavalContactProbe.Inspect(Array.Empty<(Guid, MissionShip)>()));
            var sent = (JArray)state["collisionDamage"]!["deathBroadcastAgentIds"]!;
            Assert.Equal(128, sent.Count);
            Assert.All(sent, id => Assert.Equal(last, id.ToObject<Guid>()));
            Assert.Equal(last, Assert.Single((JArray)state["collisionDamage"]!["receivedDeathAgentIds"]!).ToObject<Guid>());
            NavalContactProbe.Clear();
            state = JObject.FromObject(NavalContactProbe.Inspect(Array.Empty<(Guid, MissionShip)>()));
            Assert.Empty((JArray)state["collisionDamage"]!["deathBroadcastAgentIds"]!);
            Assert.Empty((JArray)state["collisionDamage"]!["receivedDeathAgentIds"]!);
        }
        finally { NavalContactProbe.Clear(); }
    }

    /// <summary>Both diagnostic patches bind to the current networking methods, rather than silently missing broadcasts.</summary>
    [Fact]
    public void DeathPatches_BindToSendAndReceive()
    {
        var harmony = new Harmony("test.naval.ramprobe");
        try
        {
            Assert.Single(harmony.CreateClassProcessor(typeof(NavalRamDeathBroadcastPatch)).Patch());
            Assert.Single(harmony.CreateClassProcessor(typeof(NavalRamDeathReceivePatch)).Patch());
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    /// <summary>A malformed staging request stops before touching mission state or moving a hull.</summary>
    [Fact]
    public void RamStage_WithoutTarget_ShowsUsage()
    {
        var result = new NavalRamStageCoopCommand(Mock.Of<INavalDebugHulls>()).ProcessCommand(Mock.Of<ICoopCommandArgs>());
        Assert.False(result.Succeeded);
        Assert.Equal("Usage: coop.debug.naval.ram_stage <shipId-prefix>", result.Output);
    }
}
#endif
