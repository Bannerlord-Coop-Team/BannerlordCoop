#if DEBUG
using Missions.Battles;
using Missions.Naval;
using NavalDLC.Missions.ShipActuators;
using NavalDLC.Missions.Objects;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabBehaviorTests : IDisposable
{
    private readonly MissionCurrentScope mission = new();
    private readonly NavalLabBehavior behavior;

    public NavalLabBehaviorTests()
    {
        Assert.False(SailWindProfile.IsSailWindProfileInitialized);
        var id = Guid.NewGuid();
        var manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() });
        behavior = new NavalLabBehavior(manifest, "A", null!, null!);
        // MountAndBlade is not publicized in this test project.
        typeof(MissionBehavior).GetProperty(nameof(MissionBehavior.Mission))!.SetValue(behavior, mission.Instance);
    }

    [Fact]
    public void OnBehaviorInitialize_MakesRealSailThrustAvailableBeforeAnyHullIsCreated()
    {
        behavior.OnBehaviorInitialize();

        Assert.Empty(behavior.Ships);
        Assert.Empty(behavior.Agents);
        Assert.True(SailWindProfile.IsSailWindProfileInitialized);
        var thrust = SailWindProfile.Instance.ComputeSailThrustValue(
            SailType.Square, Vec2.Forward, Vec2.Forward, Vec2.Forward);
        Assert.True(float.IsFinite(thrust));
        Assert.True(thrust > 0f);
        Assert.True(mission.Instance.IsNavalBattle);
        Assert.True(mission.Instance.DisableDying);
    }

    [Fact]
    public void OnBehaviorInitialize_ReusesProfileUntilMissionStateFinalized()
    {
        SailWindProfile.InitializeProfile();
        var existing = SailWindProfile.Instance;
        behavior.OnBehaviorInitialize();
        Assert.Same(existing, SailWindProfile.Instance);

        behavior.OnEndMissionInternal();
        Assert.Same(existing, SailWindProfile.Instance);
        behavior.OnMissionStateFinalized();
        Assert.False(SailWindProfile.IsSailWindProfileInitialized);
    }

    [Fact]
    public void StartupInventory_ReportsProfileStateEvenWhenSceneEnumerationFails()
    {
        behavior.RecordStartup("before_initialization");
        var before = JObject.FromObject(behavior.StartupDiagnostics);
        Assert.False((bool)before["sailWindProfileInitialized"]!);

        behavior.OnBehaviorInitialize();
        behavior.RecordStartup("scene_loaded");
        var after = JObject.FromObject(behavior.StartupDiagnostics);
        Assert.Equal("scene_loaded", (string?)after["phase"]);
        Assert.True((bool)after["sailWindProfileInitialized"]!);
        Assert.NotNull(after["inventoryFailure"]);
        Assert.Null(behavior.Blocker);
        Assert.Empty(behavior.Ships);
    }

    [Fact]
    public void Inspect_InvalidNativeHandlesRemainDiagnosticRowsWithoutNativeCalls()
    {
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        Assert.Equal("ship_not_created", behavior.InspectShip(null!, 0).Error);
        Assert.Equal("ship_not_created", (string?)JObject.FromObject(behavior.InspectShipInventory(null!))["error"]);
        Assert.Equal("invalid_agent", behavior.InspectAgent(null!, 0).Error);
        Assert.Equal("invalid_agent", behavior.InspectAgent(agent, 1).Error);
        Assert.Null(behavior.Blocker);
    }

    [Fact]
    public void Inspect_PartialFixturePreservesEveryAllocatedSlotAndExpectedCounts()
    {
        behavior.Ships = new MissionShip[2];
        behavior.Agents = new Agent[10];
        var json = JObject.FromObject(behavior.Inspect());
        Assert.Equal(2, (int)json["expectedShipCount"]!);
        Assert.Equal(10, (int)json["expectedAgentCount"]!);
        Assert.Equal(2, json["ships"]!.Count());
        Assert.Equal(10, json["agents"]!.Count());
        Assert.All(json["ships"]!, row => Assert.Equal("ship_not_created", (string?)row["error"]));
        Assert.All(json["agents"]!, row => Assert.Equal("invalid_agent", (string?)row["error"]));
        Assert.Null(behavior.Blocker);
    }

    [Fact]
    public void AgentPulse_ExpiredOrUnavailableHandlesAreClearedWithoutNativeInput()
    {
        Assert.StartsWith("rejected:", behavior.StartAgentControl("walk", 0, 1));
        behavior.controlledAgent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        behavior.controlDeadline = 0;
        behavior.TickAgentControl(0.1f);
        Assert.Null(behavior.controlledAgent);
        behavior.CancelControls();
        behavior.CancelControls();
        Assert.Null(behavior.Blocker);
    }

    public void Dispose()
    {
        if (SailWindProfile.IsSailWindProfileInitialized) SailWindProfile.FinalizeProfile();
        mission.Dispose();
    }
}
#endif
