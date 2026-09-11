#if DEBUG
using HarmonyLib;
using Missions.Battles;
using Missions.Naval;
using Moq;
using NavalDLC.Missions.AI.Tactics;
using NavalDLC.Missions.AI.TeamAI;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabSingleClientTeamAITests : IDisposable
{
    private readonly Harmony harmony = new("coop.tests.naval.single.teamai");
    private readonly MissionCurrentScope scope = new();
    private readonly NavalLabBehavior fixture;
    private readonly NavalShipsLogic ships;
    private readonly NavalLabBattlePowerCalculationLogic power;

    public NavalLabSingleClientTeamAITests()
    {
        Patch(typeof(SoundEvent), nameof(SoundEvent.GetEventIdFromString), nameof(ZeroInt));
        Patch(typeof(MBAnimation), nameof(MBAnimation.GetActionCodeWithName), nameof(ZeroInt));
        Patch(typeof(Scene), nameof(Scene.GetNavmeshFaceCountBetweenTwoIds), nameof(ZeroInt));
        Patch(typeof(MBCommon), nameof(MBCommon.GetTotalMissionTime), nameof(ZeroTime));
        Patch(typeof(Team), nameof(Team.IsEnemyOf), nameof(NoEnemies));
        harmony.Patch(AccessTools.Method(typeof(Mission.TeamCollection), nameof(Mission.TeamCollection.Add),
            new[] { typeof(BattleSideEnum), typeof(uint), typeof(uint), typeof(Banner), typeof(bool), typeof(bool), typeof(bool) }),
            prefix: new HarmonyMethod(GetType(), nameof(AddTeamAtEngineBoundary)));
        harmony.Patch(AccessTools.PropertySetter(typeof(Mission.TeamCollection), nameof(Mission.TeamCollection.Player)),
            prefix: new HarmonyMethod(GetType(), nameof(SetPlayerAtEngineBoundary)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(WeakGameEntity), nameof(WeakGameEntity.GlobalPosition)),
            prefix: new HarmonyMethod(GetType(), nameof(ShipPosition)));
        var id = Guid.NewGuid();
        fixture = new NavalLabBehavior(new NavalLabManifest("naval-lab:" + id.ToString("N"), id,
            new[] { "A" }, Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray(),
            new[] { Guid.NewGuid() }, NavalLabMode.SingleClientNative), "A", null!, null!);
        ships = new NavalShipsLogic();
        power = new NavalLabBattlePowerCalculationLogic(fixture);
        var behaviors = new List<MissionBehavior> { ships, new NavalAgentsLogic(), fixture, power };
        foreach (var behavior in behaviors)
            AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(behavior, new object[] { scope.Instance });
        Set(scope.Instance, "<MissionBehaviors>k__BackingField", behaviors);
        Set(scope.Instance, "<Teams>k__BackingField", new Mission.TeamCollection(scope.Instance));
        Set(scope.Instance, "_activeMissionObjects", new MBList<MissionObject>());
        Set(scope.Instance, "_listeners", new List<IMissionListener>());
        Set(scope.Instance, "<Scene>k__BackingField", CreateSceneAtEngineBoundary());
        Set(scope.Instance, "<MissionTimeTracker>k__BackingField", new MissionTimeTracker());
        fixture.InitializeNativeTeams();
        Set(scope.Instance, "<CurrentState>k__BackingField", Mission.State.Continuing);
    }

    [Fact]
    public void ProductionInitialization_RegistersStockTacticsBeforePopulatingThePlayerFormation()
    {
        Assert.Equal(2, scope.Instance.Teams.Count);
        var attacker = scope.Instance.Teams[0];
        var defender = scope.Instance.Teams[1];
        Assert.Same(attacker, scope.Instance.PlayerTeam);
        Assert.Equal(BattleSideEnum.Attacker, attacker.Side);
        Assert.Equal(BattleSideEnum.Defender, defender.Side);
        Assert.True(attacker.IsPlayerGeneral);
        Assert.IsType<TacticNavalBalancedOffense>(Assert.Single(Tactics(attacker)));
        Assert.Collection(Tactics(defender), tactic => Assert.IsType<TacticNavalBalancedOffense>(tactic),
            tactic => Assert.IsType<TacticNavalLineDefense>(tactic));
        Assert.Empty(attacker.ActiveAgents);
        Assert.Empty(defender.ActiveAgents);
        Assert.False(scope.Instance.AllowAiTicking);
    }

    [Fact]
    public void EmptyFixture_StockPowerQueriesHandleZeroTotalsOnBothTeams()
    {
        foreach (var team in scope.Instance.Teams)
        {
            Assert.Equal(0f, power.GetTotalTeamPower(team));
            Assert.Equal(1f, team.QuerySystem.TotalPowerRatio);
            MakeDecision(team);
        }
    }

    [Fact]
    public void MissingTactics_ReproducesDumpExceptionInRealDecisionWithFiveAgents()
    {
        var team = PopulatePlayerShip();
        team.TeamAI.ClearTacticOptions();
        Assert.True(team.HasAnyFormationsIncludingSpecialThatIsNotEmpty());
        var exception = Assert.Throws<TargetInvocationException>(() => MakeDecision(team));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("Sequence contains no elements", exception.InnerException!.Message);
        Assert.Contains("MaxBy", exception.InnerException.StackTrace);
    }

    [Fact]
    public void StockDecisionAndTacticTick_WithFiveAgentsAndNoEnemy_UseFixturePowerAndRemainNative()
    {
        var team = PopulatePlayerShip();
        var enemy = scope.Instance.Teams[1];
        Assert.Equal(5, team.FormationsIncludingEmpty[0].CountOfUnits);
        Assert.Equal(5, team.ActiveAgents.Count);
        Assert.Empty(enemy.ActiveAgents);
        float expected = fixture.Agents.Sum(agent => agent.Character.GetPower());
        Assert.True(expected > 0f);
        Assert.Equal(expected, power.GetTotalTeamPower(team));
        Assert.Equal(0f, power.GetTotalTeamPower(enemy));
        Assert.Equal(expected + 1f, team.QuerySystem.TotalPowerRatio);
        var ai = Assert.IsType<TeamAINavalComponent>(team.TeamAI);
        Assert.Single(ai.TeamNavalQuerySystem.FormationsInShipsInLeftToRightOrder);
        Assert.Empty(ai.TeamNavalQuerySystem.EnemyShipsWithFormationsInLeftToRightOrder);
        MakeDecision(team);
        var selected = Assert.IsType<TacticNavalBalancedOffense>(AccessTools.Field(typeof(TeamAIComponent), "_currentTactic").GetValue(ai));
        Assert.Same(Tactics(team)[0], selected);
        Assert.Equal(expected + 1f, (float)AccessTools.Method(typeof(TacticNavalBalancedOffense), "GetTacticWeight").Invoke(selected, null)!);
        selected.TickOccasionally();
        MakeDecision(team);
        Assert.Same(selected, AccessTools.Field(typeof(TeamAIComponent), "_currentTactic").GetValue(ai));
        Assert.False(team.FormationsIncludingEmpty[0].IsAIControlled);
        MakeDecision(enemy);
        Assert.Null(AccessTools.Field(typeof(TeamAIComponent), "_currentTactic").GetValue(enemy.TeamAI));
    }

    [Fact]
    public void MissingPowerProvider_IsNotMaskedByMockingStockTacticWeights()
    {
        var team = PopulatePlayerShip();
        scope.Instance.MissionBehaviors.Remove(power);
        var exception = Assert.Throws<TargetInvocationException>(() => MakeDecision(team));
        Assert.IsType<NullReferenceException>(exception.InnerException);
        Assert.Contains("TeamQuerySystem", exception.InnerException!.StackTrace);
    }

    // Native spawn/arrangement storage is substituted; membership, counts, queries and tactics are real.
    private Team PopulatePlayerShip()
    {
        var team = scope.Instance.PlayerTeam;
        var formation = Shell<Formation>();
        Set(formation, "Team", team);
        Set(formation, "_arrangement", Mock.Of<IFormationArrangement>(arrangement => arrangement.UnitCount == 0));
        var agents = Enumerable.Range(0, 5).Select(index =>
        {
            var agent = Shell<Agent>();
            var character = new BasicCharacterObject();
            Set(character, "<Level>k__BackingField", 10 + index);
            Set(agent, "_character", character);
            Set(agent, "<Team>k__BackingField", team);
            Set(agent, "_formation", formation);
            return agent;
        }).ToArray();
        Set(formation, "_detachedUnits", new MBList<Agent>(agents));
        team.FormationsIncludingEmpty.Add(formation);
        team.FormationsIncludingSpecialAndEmpty.Add(formation);
        foreach (var agent in agents) team.AddAgentToTeam(agent);
        fixture.Agents = agents;
        var ship = Shell<MissionShip>();
        Set(ship, "<Formation>k__BackingField", formation);
        var assignment = ships.GetShipAssignment(team.TeamSide, formation.FormationIndex);
        Set(assignment, "<MissionShip>k__BackingField", ship);
        Set(assignment, "<ShipOrigin>k__BackingField", Mock.Of<IShipOrigin>());
        Set(assignment, "<MissionShipObject>k__BackingField", Shell<MissionShipObject>());
        return team;
    }

    private static List<TacticComponent> Tactics(Team team) =>
        (List<TacticComponent>)AccessTools.Field(typeof(TeamAIComponent), "_availableTactics").GetValue(team.TeamAI)!;
    private static void MakeDecision(Team team) => AccessTools.Method(typeof(TeamAIComponent), "MakeDecision").Invoke(team.TeamAI, null);
    private void Patch(Type type, string method, string prefix) =>
        harmony.Patch(AccessTools.Method(type, method), prefix: new HarmonyMethod(GetType(), prefix));
    private static void Set(object instance, string field, object value) => AccessTools.Field(instance.GetType(), field).SetValue(instance, value);
    private static T Shell<T>()
    {
        var managed = typeof(ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var moduleTypes = AccessTools.Field(managed, "_moduleTypes");
        var previous = moduleTypes.GetValue(null);
        try
        {
            if (previous == null) moduleTypes.SetValue(null, new Dictionary<string, Type>());
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }
        finally { moduleTypes.SetValue(null, previous); }
    }
    private static Scene CreateSceneAtEngineBoundary()
    {
        var assembly = typeof(TaleWorlds.DotNet.NativeObject).Assembly;
        var field = AccessTools.Field(assembly.GetType("TaleWorlds.DotNet.LibraryApplicationInterface"), "IManaged");
        var previous = field.GetValue(null);
        try
        {
            field.SetValue(null, typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create))!
                .MakeGenericMethod(field.FieldType, typeof(NativeReferenceBoundary)).Invoke(null, null));
            var scene = Shell<Scene>();
            GC.SuppressFinalize(scene);
            return scene;
        }
        finally { field.SetValue(null, previous); }
    }
    public class NativeReferenceBoundary : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod!.ReturnType == typeof(int) ? 0 : null;
    }
    private static bool ZeroInt(ref int __result) { __result = 0; return false; }
    private static bool ZeroTime(ref float __result) { __result = 0f; return false; }
    private static bool NoEnemies(ref bool __result) { __result = false; return false; }
    private static bool ShipPosition(ref Vec3 __result) { __result = new Vec3(250f, 250f, 0f); return false; }
    private static bool SetPlayerAtEngineBoundary(Mission.TeamCollection __instance, Team value)
    {
        Set(__instance, "_playerTeam", value);
        return false;
    }
    private static bool AddTeamAtEngineBoundary(Mission.TeamCollection __instance, BattleSideEnum side, ref Team __result)
    {
        var team = Shell<Team>();
        Set(team, "<Mission>k__BackingField", Mission.Current);
        Set(team, "<Side>k__BackingField", side);
        Set(team, "<FormationsIncludingEmpty>k__BackingField", new MBList<Formation>());
        Set(team, "<FormationsIncludingSpecialAndEmpty>k__BackingField", new MBList<Formation>());
        Set(team, "_activeAgents", new MBList<Agent>());
        Set(team, "_teamAgents", new MBList<Agent>());
        Set(team, "_orderControllers", new List<OrderController>());
        Set(team, "<QuerySystem>k__BackingField", new TeamQuerySystem(team));
        ((List<Team>)__instance).Add(team);
        __result = team;
        return false;
    }

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        scope.Dispose();
    }
}
#endif
