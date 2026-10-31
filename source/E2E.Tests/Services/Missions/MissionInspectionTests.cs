#if DEBUG
using Common.Commands;
using Common.Util;
using Missions.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class MissionInspectionRegistrationTests : MissionTestEnvironment
{
    public MissionInspectionRegistrationTests(ITestOutputHelper output) : base(output, 1) { }

    [Fact]
    public void ActualContainers_RegisterFourReadOnlyCommands_AndReadNoMissionOnGameThread()
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.IsType<MissionInspection>(instance.Resolve<IMissionInspection>());
                Assert.NotSame(instance.Resolve<IMissionInspection>(), instance.Resolve<IMissionInspection>());
                var registry = instance.Resolve<ICoopCommandRegistry>();
                var commands = registry.Commands.Where(command => command.Prefix == "coop.debug.mission").ToArray();
                Assert.Equal(4, commands.Length);
                foreach (var command in commands)
                {
                    Assert.Equal(CoopCommandSide.Both, command.Side);
                    var result = registry.ProcessCommand(command.FullName, new CoopCommandArgsFactory().FromValues(Array.Empty<string>()));
                    Assert.True(result.Succeeded, result.Output);
                    Assert.Contains("unavailable:no_mission", result.Output);
                }
            });
        }
    }
}

public sealed class MissionInspectionTests
{
    [Theory]
    [InlineData(MissionInspectionSlice.Summary)]
    [InlineData(MissionInspectionSlice.Camera)]
    [InlineData(MissionInspectionSlice.Agents)]
    [InlineData(MissionInspectionSlice.Views)]
    public void MissingAndFinalizedMission_NeverReadNativeState(MissionInspectionSlice slice)
    {
        var inspection = new MissionInspection();
        Assert.Contains("unavailable:no_mission", JsonConvert.SerializeObject(inspection.ReadMission(null!, slice, 0, 8)));
        var shell = ObjectHelper.SkipConstructor<Mission>();
        Assert.Contains("unavailable:finalized_mission", JsonConvert.SerializeObject(inspection.ReadMission(shell, slice, 0, 8)));
        Assert.Equal(UIntPtr.Zero, shell.Pointer);
        Assert.Null(shell.Scene);
    }

    [Theory]
    [InlineData(Mission.State.Initializing)]
    [InlineData(Mission.State.EndingNextFrame)]
    [InlineData(Mission.State.Over)]
    public void NonContinuingMission_DoesNotReadSceneOrAgentNativeGetters(Mission.State state)
    {
        var shell = ObjectHelper.SkipConstructor<Mission>();
        shell.Pointer = new UIntPtr(1);
        shell.CurrentState = state;
        var result = new MissionInspection().ReadMission(shell, MissionInspectionSlice.Camera, 0, 8);
        Assert.Contains("unavailable:mission_not_continuing", JsonConvert.SerializeObject(result));
        Assert.Equal(state, shell.CurrentState);
    }

    [Fact]
    public void AgentPages_AreBounded_AndInvalidAgentPointersAreNotDereferencedOrMutated()
    {
        var shell = ObjectHelper.SkipConstructor<Mission>();
        shell.Pointer = new UIntPtr(1);
        shell.CurrentState = Mission.State.Continuing;
        var agents = Enumerable.Range(0, 35).Select(_ => ObjectHelper.SkipConstructor<Agent>()).ToArray();
        shell._allAgents = new AgentList(agents);
        var inspection = new MissionInspection();
        var page = JObject.FromObject(inspection.ReadMission(shell, MissionInspectionSlice.Agents, 16, 16));
        Assert.Equal(16, ((JArray)page["rows"]!).Count);
        Assert.Equal(32, (int)page["nextOffset"]!);
        Assert.All(page["rows"]!, row => Assert.Equal("unavailable:agent_lifetime", (string?)row["status"]));
        var last = JObject.FromObject(inspection.ReadMission(shell, MissionInspectionSlice.Agents, 32, 16));
        Assert.Equal(3, ((JArray)last["rows"]!).Count);
        Assert.Equal(JTokenType.Null, last["nextOffset"]!.Type);
        var empty = JObject.FromObject(inspection.ReadMission(shell, MissionInspectionSlice.Agents, 100000, 16));
        Assert.Empty((JArray)empty["rows"]!);
        Assert.Equal(35, shell.AllAgents.Count);
        Assert.All(agents, agent => Assert.Equal(UIntPtr.Zero, agent.Pointer));
        Assert.Equal(Mission.State.Continuing, shell.CurrentState);
    }

    [Fact]
    public void ViewInventory_PagesManagedTypesWithoutInvokingBehaviors_AndMissingScreenIsUnavailable()
    {
        var shell = ObjectHelper.SkipConstructor<Mission>();
        shell.Pointer = new UIntPtr(1);
        shell.CurrentState = Mission.State.Continuing;
        var behaviors = Enumerable.Range(0, 20).Select(_ => (MissionBehavior)new InspectionTestLogic()).ToList();
        HarmonyLib.AccessTools.Field(typeof(Mission), "<MissionBehaviors>k__BackingField").SetValue(shell, behaviors);
        var inspection = new MissionInspection();
        var first = JsonConvert.SerializeObject(inspection.ReadMission(shell, MissionInspectionSlice.Views, 0, 16));
        var second = JsonConvert.SerializeObject(inspection.ReadMission(shell, MissionInspectionSlice.Views, 0, 16));
        Assert.Equal(first, second);
        var page = JObject.Parse(first);
        Assert.Equal(16, ((JArray)page["rows"]!).Count);
        Assert.Equal(16, (int)page["nextOffset"]!);
        Assert.All(page["rows"]!, row => Assert.False((bool)row["isView"]!));
        var viewType = System.Reflection.Assembly.Load("TaleWorlds.MountAndBlade.View")
            .GetType("TaleWorlds.MountAndBlade.View.MissionViews.MissionMainAgentController")!;
        behaviors[0] = (MissionBehavior)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(viewType);
        var unbound = JObject.FromObject(inspection.ReadMission(shell, MissionInspectionSlice.Views, 0, 1));
        Assert.True((bool)unbound["rows"]![0]!["isView"]!);
        Assert.False((bool)unbound["rows"]![0]!["screenMatches"]!);
        Assert.Equal(20, behaviors.Count);
        Assert.Contains("unavailable:no_active_mission_screen", JsonConvert.SerializeObject(
            inspection.ReadMission(shell, MissionInspectionSlice.Camera, 0, 8)));
    }

    [Theory]
    [InlineData("-1", "8")]
    [InlineData("100001", "8")]
    [InlineData("0", "17")]
    [InlineData("0", "0")]
    [InlineData("2147483648", "8")]
    [InlineData("NaN", "8")]
    public void InvalidPages_DoNotCallInspection(string offset, string limit)
    {
        var reader = new RecordingInspection();
        var result = new MissionAgentsCommand(reader).ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { offset, limit }));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public void DefaultsAndMaximumPage_UseFiniteScalarProtocol_AndEnforceOutputCap()
    {
        var reader = new RecordingInspection();
        var command = new MissionAgentsCommand(reader);
        var args = new CoopCommandArgsFactory();
        var result = command.ProcessCommand(args.FromValues(Array.Empty<string>()));
        Assert.StartsWith("LIVE_TEST_JSON=", result.Output);
        Assert.Equal((0, 8), (reader.Offset, reader.Limit));
        Assert.True(command.ProcessCommand(args.FromValues(new[] { "100000", "16" })).Succeeded);
        Assert.Equal((100000, 16), (reader.Offset, reader.Limit));
        reader.Result = new string('x', 24576);
        Assert.Equal("output_limit", command.ProcessCommand(args.FromValues(Array.Empty<string>())).ErrorCode);
    }

    [Fact]
    public void VectorSerialization_ContainsOnlyFiniteComponents_AndExplicitUnavailableStatus()
    {
        var dto = new MissionInspectionVector(new Vec3(float.NaN, float.PositiveInfinity, float.NegativeInfinity));
        var json = JsonConvert.SerializeObject(dto);
        Assert.DoesNotContain("NaN", json);
        Assert.DoesNotContain("Infinity", json);
        Assert.Equal(JTokenType.Null, JObject.Parse(json)["X"]!.Type);
        Assert.Contains("unavailable:nonfinite_component", json);
        var finite = JObject.FromObject(new MissionInspectionVector(new Vec3(float.MaxValue, -2, 3)));
        Assert.Equal(float.MaxValue, (float)finite["X"]!);
        Assert.Equal(4, finite.Properties().Count());
    }

    private sealed class InspectionTestLogic : MissionLogic { }

    private sealed class RecordingInspection : IMissionInspection
    {
        public int Calls, Offset, Limit;
        public object Result = new { status = "observed" };
        public object Read(MissionInspectionSlice slice, int offset, int limit)
        {
            Calls++;
            Offset = offset;
            Limit = limit;
            return Result;
        }
    }
}
#endif
