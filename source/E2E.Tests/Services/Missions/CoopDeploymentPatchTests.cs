using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Common;
using Common.Commands;
using Common.Util;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>Exercises the deployment patch with synthetic managed objects, without native battle teardown.</summary>
public class CoopDeploymentPatchTests
{
    [Fact]
    public void CoopEmptyTeamDeploymentPatch_DoesNotStoreTeamsOrMissionsInStaticFields()
    {
        var owningFields = GetPatchType()
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => StoresMissionGraph(field.FieldType, new HashSet<Type>()))
            .Select(field => field.Name)
            .ToArray();

        Assert.Empty(owningFields);
    }

    [Fact]
    public void CoopEmptyTeamDeploymentPatch_IsDiscoverableByPatchAll_AndHooksItsTargets()
    {
        var harmony = new Harmony("e2e.coopdeploy.patchtest");
        try
        {
            var patched = harmony.CreateClassProcessor(GetPatchType()).Patch() ?? new List<MethodInfo>();
            Assert.Contains(patched, method => method.Name.Contains("IsPlanMade"));
            Assert.Contains(patched, method => method.Name.Contains("MakeTeamPlans"));
            var makePlans = AccessTools.Method(typeof(DefaultBattleMissionAgentSpawnLogic), "MakeTeamPlans");
            Assert.Contains(Harmony.GetPatchInfo(makePlans).Finalizers,
                patch => patch.owner == harmony.Id && patch.PatchMethod.Name == "MakeTeamPlans_Finalizer");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Theory]
    [InlineData("existing_plan")]
    [InlineData("making_plans")]
    [InlineData("disabled")]
    [InlineData("inactive_battle")]
    [InlineData("null_team")]
    [InlineData("no_mission")]
    [InlineData("player_team")]
    public void IsPlanMade_PreservesResultWhenOverrideIsIneligible(string reason)
    {
        using var state = new PatchState();
        var team = CreateForeignTeam();
        bool original = reason == "existing_plan";
        if (reason == "making_plans") InvokePatch("MakeTeamPlans_Prefix");
        if (reason == "disabled") BattleSpawnConfig.Enabled = false;
        if (reason == "inactive_battle") SetBattleActive(false);
        if (reason == "null_team") team = null;
        if (reason == "no_mission") Mission._current = null;
        if (reason == "player_team") team = Mission.Current.PlayerTeam;

        Assert.Equal(original, ApplyPlanPostfix(team, original));
#if DEBUG
        var status = ReadStatus();
        Assert.Equal(0, status.Value<long>("overrideCalls"));
        Assert.Equal(0, status.Value<int>("totalObservedTeams"));
        Assert.Equal(0, status.Value<int>("totalObservedMissions"));
#endif
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void IsPlanMade_OverridesForeignTeamWithOrWithoutActiveAgents(int activeAgents)
    {
        using var state = new PatchState();
        var team = CreateForeignTeam();
        for (int index = 0; index < activeAgents; index++)
            team._activeAgents.Add(CreateShell<Agent>());

        Assert.True(ApplyPlanPostfix(team));
        Assert.True(ApplyPlanPostfix(team));
#if DEBUG
        var status = ReadStatus();
        Assert.Equal(1, status.Value<int>("totalObservedTeams"));
        Assert.Equal(1, status.Value<int>("aliveTeams"));
        Assert.Equal(1, status.Value<int>("totalObservedMissions"));
        Assert.Equal(1, status.Value<int>("aliveMissions"));
        Assert.Equal(2, status.Value<long>("overrideCalls"));
        Assert.Equal(activeAgents == 0 ? 2 : 0, status.Value<long>("emptyTeamOverrideCalls"));
        Assert.Equal("Attacker", status.Value<string>("lastOverrideSide"));
        Assert.Equal(activeAgents, status.Value<int>("lastOverrideActiveAgents"));
#endif
        GC.KeepAlive(team);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MakeTeamPlans_FinalizerRestoresOverrideAfterNormalOrExceptionalExit(bool throws)
    {
        using var state = new PatchState();
        var team = CreateForeignTeam();
        var harmony = new Harmony("e2e.coopdeploy.finalizertest");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(CoopDeploymentPatchTests), nameof(SyntheticMakeTeamPlans)),
                prefix: new HarmonyMethod(GetPatchMethod("MakeTeamPlans_Prefix")),
                finalizer: new HarmonyMethod(GetPatchMethod("MakeTeamPlans_Finalizer")));
            if (throws)
                Assert.Throws<InvalidOperationException>(() => SyntheticMakeTeamPlans(team, true));
            else
                SyntheticMakeTeamPlans(team, false);

            Assert.True(ApplyPlanPostfix(team));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

#if DEBUG
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetentionCommand_DefaultStatusReportsSourceAndZeroObservations(bool isServer)
    {
        using var state = new PatchState();
        ModInformation.IsServer = isServer;
        var command = CreateCommand();
        Assert.Equal("coop.debug.map_event", command.Prefix);
        Assert.Equal("deployment_retention_state", command.Name);
        Assert.Equal(CoopCommandSide.Both, command.Side);
        Assert.False(Assert.Single(command.ExpectedArgs).IsRequired);

        var status = ReadStatus();
        Assert.Equal(ReadStatus("status").ToString(), status.ToString());
        Assert.Equal(1, status.Value<int>("schemaVersion"));
        Assert.Equal(GetPatchType().Assembly.ManifestModule.ModuleVersionId,
            Guid.Parse(status.Value<string>("sourceModuleVersionId")!));
        Assert.Equal(isServer ? "server" : "client", status.Value<string>("role"));
        Assert.Equal(0, status.Value<int>("totalObservedTeams"));
        Assert.Equal(0, status.Value<int>("totalObservedMissions"));
        Assert.Equal(0, status.Value<long>("overrideCalls"));
        Assert.False(status.Value<bool>("currentMissionActive"));
        Assert.False(status.Value<bool>("coopBattleActive"));
        Assert.False(status.Value<bool>("collectionRequested"));
        Assert.False(status.Value<bool>("resetPerformed"));
        Assert.Null(status["passed"]);
    }

    [Theory]
    [InlineData("collect", true, false)]
    [InlineData("collect", false, true)]
    [InlineData("collect", true, true)]
    [InlineData("reset", true, false)]
    [InlineData("reset", false, true)]
    [InlineData("reset", true, true)]
    public void RetentionCommand_RefusesCollectionOrResetWhileMissionOrBattleIsActive(
        string action, bool missionActive, bool battleActive)
    {
        using var state = new PatchState();
        var team = CreateForeignTeam();
        Assert.True(ApplyPlanPostfix(team));
        if (!missionActive) Mission._current = null;
        SetBattleActive(battleActive);
        var before = ReadStatus();

        var result = RunCommand(action);

        Assert.False(result.Succeeded);
        Assert.Equal("battle_active", result.ErrorCode);
        Assert.Equal(before.ToString(), ReadStatus().ToString());
        GC.KeepAlive(team);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("status", "collect")]
    public void RetentionCommand_RejectsInvalidArguments(params string[] args)
    {
        using var state = new PatchState();
        var result = RunCommand(args);
        Assert.False(result.Succeeded);
        Assert.Equal("invalid_arguments", result.ErrorCode);
    }

    [Fact]
    public void RetentionCommand_ReleasedSyntheticMissionGraphsAreCollectibleWhileOutputsRemainAlive()
    {
        using var state = new PatchState();
        var observations = Enumerable.Range(0, 3).Select(_ => ObserveAndRelease()).ToArray();
        var status = ReadStatus("collect");
        Assert.True(status.Value<bool>("collectionRequested"));
        Assert.False(status.Value<bool>("resetPerformed"));

        Assert.All(observations, observation =>
        {
            Assert.False(observation.Team.IsAlive);
            Assert.False(observation.Mission.IsAlive);
            Assert.Contains("LIVE_TEST_JSON=", observation.Output);
        });
        Assert.Equal(3, status.Value<int>("totalObservedTeams"));
        Assert.Equal(3, status.Value<int>("totalObservedMissions"));
        Assert.Equal(6, status.Value<long>("overrideCalls"));
        Assert.Equal(6, status.Value<long>("emptyTeamOverrideCalls"));
        Assert.Equal(0, status.Value<int>("aliveTeams"));
        Assert.Equal(0, status.Value<int>("aliveMissions"));
        Assert.False(status.Value<bool>("currentMissionActive"));
        Assert.False(status.Value<bool>("coopBattleActive"));
        GC.KeepAlive(observations);

        var reset = ReadStatus("reset");
        Assert.True(reset.Value<bool>("resetPerformed"));
        Assert.False(reset.Value<bool>("collectionRequested"));
        Assert.Equal(0, reset.Value<int>("totalObservedTeams"));
        Assert.Equal(0, reset.Value<int>("totalObservedMissions"));
        Assert.Equal(0, reset.Value<long>("overrideCalls"));
        Assert.Equal(0, reset.Value<long>("emptyTeamOverrideCalls"));
        Assert.Null(reset.Value<string>("lastOverrideSide"));
    }

    [Fact]
    public void RetentionCommand_ResetPrunesCollectedTargetsBeforeClearingCounters()
    {
        using var state = new PatchState();
        var observation = ObserveAndRelease();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(observation.Team.IsAlive);
        Assert.False(observation.Mission.IsAlive);

        var reset = ReadStatus("reset");

        Assert.True(reset.Value<bool>("resetPerformed"));
        Assert.Equal(0, reset.Value<int>("totalObservedTeams"));
        Assert.Equal(0, reset.Value<int>("totalObservedMissions"));
        Assert.Equal(0, reset.Value<int>("aliveTeams"));
        Assert.Equal(0, reset.Value<int>("aliveMissions"));
        Assert.Equal(0, reset.Value<long>("overrideCalls"));
        GC.KeepAlive(observation.Output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetentionCommand_ResetRefusesEitherSurvivingTargetWithoutErasingEvidence(bool retainTeam)
    {
        using var state = new PatchState();
        object retained = ObserveSingleRetainedTarget(retainTeam);
        ReadStatus("collect");
        var before = ReadStatus();
        Assert.Equal(retainTeam ? 1 : 0, before.Value<int>("aliveTeams"));
        Assert.Equal(retainTeam ? 0 : 1, before.Value<int>("aliveMissions"));

        var result = RunCommand("reset");

        Assert.False(result.Succeeded);
        Assert.Equal("observations_alive", result.ErrorCode);
        Assert.Equal(before.ToString(), ReadStatus().ToString());
        GC.KeepAlive(retained);
    }

    [Fact]
    public void RetentionCommand_ObservesTeamOwningMissionInsteadOfCurrentMission()
    {
        using var state = new PatchState();
        var team = CreateForeignTeam();
        var owningMission = team.Mission;
        CreateForeignTeam();

        Assert.True(ApplyPlanPostfix(team));

        var references = (List<WeakReference>)GetPatchType()
            .GetField("observedMissions", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Assert.Same(owningMission, Assert.Single(references).Target);
        Assert.NotSame(Mission.Current, Assert.Single(references).Target);
        GC.KeepAlive(team);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Team, WeakReference Mission, string Output) ObserveAndRelease()
    {
        var team = CreateForeignTeam();
        Assert.True(ApplyPlanPostfix(team));
        Assert.True(ApplyPlanPostfix(team));
        var output = RunCommand("status");
        Assert.True(output.Succeeded);
        var observed = (new WeakReference(team), new WeakReference(team.Mission), output.Output);
        Mission._current = null;
        SetBattleActive(false);
        return observed;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object ObserveSingleRetainedTarget(bool retainTeam)
    {
        var team = CreateForeignTeam();
        var mission = team.Mission;
        Assert.True(ApplyPlanPostfix(team));
        ((List<Team>)mission.Teams).Clear();
        mission.Teams._playerTeam = null;
        SetTeamProperty(team, nameof(Team.Mission), null);
        Mission._current = null;
        SetBattleActive(false);
        return retainTeam ? team : mission;
    }

    private static ICoopCommand CreateCommand()
    {
        var type = GetPatchType().GetNestedType("DeploymentRetentionStateCoopCommand", BindingFlags.Public);
        Assert.NotNull(type);
        return Assert.IsAssignableFrom<ICoopCommand>(Activator.CreateInstance(type!));
    }

    private static CoopCommandResult RunCommand(params string[] args) =>
        CreateCommand().ProcessCommand(new CoopCommandArgsFactory().FromValues(args));

    private static JObject ReadStatus(params string[] args)
    {
        var result = RunCommand(args);
        Assert.True(result.Succeeded, result.Output);
        Assert.StartsWith("LIVE_TEST_JSON=", result.Output);
        return JObject.Parse(result.Output.Substring("LIVE_TEST_JSON=".Length));
    }
#endif

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SyntheticMakeTeamPlans(Team team, bool throws)
    {
        Assert.False(ApplyPlanPostfix(team));
        if (throws) throw new InvalidOperationException("synthetic plan failure");
    }

    private static Team CreateForeignTeam()
    {
        var mission = CreateShell<Mission>();
        mission.Teams = new Mission.TeamCollection(mission);
        var playerTeam = CreateShell<Team>();
        mission.Teams._playerTeam = playerTeam;
        var team = CreateShell<Team>();
        team._activeAgents = new MBList<Agent>();
        SetTeamProperty(team, nameof(Team.Mission), mission);
        SetTeamProperty(team, nameof(Team.Side), BattleSideEnum.Attacker);
        ((List<Team>)mission.Teams).Add(team);
        Mission._current = mission;
        SetBattleActive(true);
        return team;
    }

    private static T CreateShell<T>() where T : class
    {
        var shell = ObjectHelper.SkipConstructor<T>();
        // No native object was created, so its native finalizer must not run.
        GC.SuppressFinalize(shell);
        return shell;
    }

    private static void SetTeamProperty(Team team, string property, object? value)
    {
        // Getter-only auto-properties have no setter to publicize.
        AccessTools.Field(typeof(Team), $"<{property}>k__BackingField").SetValue(team, value);
    }

    private static bool ApplyPlanPostfix(Team? team, bool original = false)
    {
        object?[] args = { team, original };
        GetPatchMethod("IsPlanMade_Postfix").Invoke(null, args);
        return (bool)args[1]!;
    }

    private static Type GetPatchType() => typeof(BattleSpawnGate).Assembly
        .GetType("GameInterface.Services.MapEvents.Patches.CoopEmptyTeamDeploymentPatch", throwOnError: true)!;

    private static MethodInfo GetPatchMethod(string name) =>
        GetPatchType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void InvokePatch(string name) => GetPatchMethod(name).Invoke(null, null);

    private static void SetBattleActive(bool active) =>
        AccessTools.Field(typeof(BattleSpawnGate), "_activeMapEventId").SetValue(null, active ? "synthetic-deployment" : null);

    private static bool StoresMissionGraph(Type type, HashSet<Type> visited)
    {
        if (type == typeof(Team) || type == typeof(Mission)) return true;
        if (type == typeof(WeakReference) ||
            (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(WeakReference<>))) return false;
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || !visited.Add(type)) return false;
        if (type.HasElementType) return StoresMissionGraph(type.GetElementType()!, visited);
        if (type.IsGenericType && type.GetGenericArguments().Any(argument => StoresMissionGraph(argument, visited)))
            return true;
        return type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(field => StoresMissionGraph(field.FieldType, visited)) ||
            (type.BaseType != null && StoresMissionGraph(type.BaseType, visited));
    }

    private sealed class PatchState : IDisposable
    {
        private readonly Mission previousMission = Mission._current;
        private readonly bool previousEnabled = BattleSpawnConfig.Enabled;
        private readonly bool previousServer = ModInformation.IsServer;
        private readonly FieldInfo battleField = AccessTools.Field(typeof(BattleSpawnGate), "_activeMapEventId");
        private readonly object? previousBattle;
        private readonly List<(FieldInfo Field, object? Value)> previousFields = new();

        public PatchState()
        {
            previousBattle = battleField.GetValue(null);
            foreach (var field in GetPatchType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                var value = field.GetValue(null);
                if (value is IList list)
                {
                    previousFields.Add((field, list.Cast<object>().ToArray()));
                    list.Clear();
                }
                else
                {
                    previousFields.Add((field, value));
                    field.SetValue(null, field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null);
                }
            }
            Mission._current = null;
            BattleSpawnConfig.Enabled = true;
            SetBattleActive(false);
        }

        public void Dispose()
        {
            Mission._current = previousMission;
            BattleSpawnConfig.Enabled = previousEnabled;
            ModInformation.IsServer = previousServer;
            battleField.SetValue(null, previousBattle);
            foreach (var entry in previousFields)
            {
                if (entry.Field.GetValue(null) is IList list)
                {
                    list.Clear();
                    foreach (var value in (object[])entry.Value!) list.Add(value);
                }
                else
                {
                    entry.Field.SetValue(null, entry.Value);
                }
            }
        }
    }
}
