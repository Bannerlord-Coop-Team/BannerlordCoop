using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Common.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Tests.Services.MapEvents;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Drives the native <c>CheckDeployment</c> spawn gate for a coop field battle whose player side also holds the
/// empty AI-ally team, with only the spawn-path geometry and agent creation replaced headless.
/// </summary>
public class CoopEmptyTeamSpawnGateTests
{
    private const int PlayerTroops = 3;

    private static readonly List<(bool IsPlayerSide, int Number)> spawnCalls = new();

    private static readonly MethodInfo CheckDeploymentMethod =
        AccessTools.Method(typeof(DefaultBattleMissionAgentSpawnLogic), "CheckDeployment");
    private static readonly MethodInfo IsPlanMadeMethod = AccessTools.Method(typeof(DefaultMissionDeploymentPlan),
        nameof(DefaultMissionDeploymentPlan.IsPlanMade), new[] { typeof(Team) });

    // A battle earlier in the process (e.g. single-player before joining) compiles the gate before coop patches
    // exist; the player's side must still deploy once the coop patch is installed.
    [Fact]
    public void PlayerSideSpawns_WhenSpawnGateCompiledBeforeCoopPatch()
    {
        using var scope = new GateScope();
        // Patching IsPlanMade stops the JIT inlining it for the rest of the process, which would hide the stale compile.
        Assert.True(Harmony.GetPatchInfo(IsPlanMadeMethod) == null,
            "IsPlanMade was patched earlier in this process, so CheckDeployment can no longer compile with it inlined");
        Assert.Equal(new[] { scope.EngineOwner }, Harmony.GetPatchInfo(CheckDeploymentMethod).Owners);
        var battle = new GateBattle();

        battle.CheckDeployment();
        Assert.Empty(spawnCalls);

        scope.InstallCoopPatch();
        scope.BattleRegistry.BeginBattle("ticket-battle");
        battle.EnablePlayerSideSpawn();
        battle.CheckDeployment();

        var spawn = Assert.Single(spawnCalls);
        Assert.True(spawn.IsPlayerSide);
        Assert.Equal(PlayerTroops, spawn.Number);
    }

    // Owns the headless engine shims, the installed coop patch and the process statics one gate run touches.
    private sealed class GateScope : IDisposable
    {
        private readonly Harmony engine = new("e2e.coop-empty-team-gate.engine");
        private readonly Harmony coop = new("e2e.coop-empty-team-gate.coop");
        private readonly Mission previousMission = Mission._current;
        private readonly bool previousEnabled = BattleSpawnConfig.Enabled;
        private readonly ActiveCoopBattleScope battleScope = new(null);
        private readonly DeploymentPatchStaticsScope patchStatics = new();
        private readonly List<(HarmonyPatchType Type, Patch Patch)> liftedPatches = new();

        public GateScope()
        {
            BattleSpawnConfig.Enabled = true;
            spawnCalls.Clear();
            LiftExistingGatePatches();

            Prefix(AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.HasSpawnPath)), nameof(HasSpawnPath));
            Prefix(AccessTools.Method(typeof(Mission), nameof(Mission.GetInitialSpawnPathData)), nameof(InitialSpawnPath));
            Prefix(AccessTools.Method(typeof(Mission), nameof(Mission.ComputeSpawnPathDeploymentOffset)), nameof(ZeroOffset));
            Prefix(AccessTools.Method(typeof(DefaultBattleMissionAgentSpawnLogic), "GetTeamSpawnPathOffsetRange"), nameof(ZeroOffset));
            Prefix(AccessTools.Method(typeof(DefaultBattleMissionAgentSpawnLogic),
                nameof(DefaultBattleMissionAgentSpawnLogic.ComputeTeamDeploymentOffsets)), nameof(TeamOffsets));
            Prefix(AccessTools.Method(typeof(MissionBattleSideSpawnContext), nameof(MissionBattleSideSpawnContext.SpawnTroops)),
                nameof(RecordSpawn));
            // Rebuilds the gate after the shims so an earlier compile can't keep inlined engine getters; IsPlanMade
            // stays unpatched here, as it is in a battle that runs before coop installs its patches.
            Prefix(CheckDeploymentMethod, nameof(RebuildGate));
        }

        /// <summary>The owner of the engine shims, the only patches the gate carries before the coop patch installs.</summary>
        public string EngineOwner => engine.Id;

        /// <summary>The client container's battle registry the coop patch resolves; no battle is open until a test begins one.</summary>
        public ICoopBattleMissionRegistry BattleRegistry => battleScope.Registry;

        // Installs the production patch class exactly as the game's PatchAll does.
        public void InstallCoopPatch() => coop.CreateClassProcessor(DeploymentPatchStaticsScope.PatchType).Patch();

        public void Dispose()
        {
            coop.UnpatchAll(coop.Id);
            engine.UnpatchAll(engine.Id);
            foreach (var lifted in liftedPatches)
                Reapply(lifted.Type, lifted.Patch);
            Mission._current = previousMission;
            BattleSpawnConfig.Enabled = previousEnabled;
            patchStatics.Dispose();
            battleScope.Dispose();
            spawnCalls.Clear();
        }

        // Earlier E2E environments leave production patches installed for the process, so the gate is taken back to
        // its pre-coop state here and those patches are put back on dispose.
        private void LiftExistingGatePatches()
        {
            var patches = Harmony.GetPatchInfo(CheckDeploymentMethod);
            if (patches == null) return;

            liftedPatches.AddRange(patches.Prefixes.Select(patch => (HarmonyPatchType.Prefix, patch)));
            liftedPatches.AddRange(patches.Postfixes.Select(patch => (HarmonyPatchType.Postfix, patch)));
            liftedPatches.AddRange(patches.Transpilers.Select(patch => (HarmonyPatchType.Transpiler, patch)));
            liftedPatches.AddRange(patches.Finalizers.Select(patch => (HarmonyPatchType.Finalizer, patch)));
            foreach (var owner in patches.Owners)
                new Harmony(owner).Unpatch(CheckDeploymentMethod, HarmonyPatchType.All, owner);
        }

        // Puts one lifted patch back on the gate under its original owner and ordering.
        private static void Reapply(HarmonyPatchType type, Patch patch)
        {
            var method = new HarmonyMethod(patch.PatchMethod, patch.priority, patch.before, patch.after, patch.debug);
            new Harmony(patch.owner).Patch(CheckDeploymentMethod,
                prefix: type == HarmonyPatchType.Prefix ? method : null,
                postfix: type == HarmonyPatchType.Postfix ? method : null,
                transpiler: type == HarmonyPatchType.Transpiler ? method : null,
                finalizer: type == HarmonyPatchType.Finalizer ? method : null);
        }

        private void Prefix(MethodBase target, string shim) =>
            engine.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(typeof(GateScope), shim)));

        private static void RebuildGate() { }

        private static bool HasSpawnPath(ref bool __result)
        {
            __result = true;
            return false;
        }

        private static bool InitialSpawnPath(ref SpawnPathData __result)
        {
            __result = Shell<SpawnPathData>();
            return false;
        }

        private static bool ZeroOffset(ref float __result)
        {
            __result = 0f;
            return false;
        }

        private static bool TeamOffsets(float[] teamOffsetRanges, out float[] teamDeployOffsets)
        {
            teamDeployOffsets = new float[teamOffsetRanges.Length];
            return false;
        }

        // Agent creation is the engine boundary; the gate's decision is the spawn request itself.
        private static bool RecordSpawn(MissionBattleSideSpawnContext __instance, int number, ref int __result)
        {
            spawnCalls.Add((__instance.IsPlayerSide, number));
            __result = number;
            return false;
        }
    }

    // The ticket's deployment state: enemy side already spawned, the player's planned team holding its full reserve,
    // and the side's AI-ally team empty, so the native plan-making never plans it.
    private sealed class GateBattle
    {
        private readonly DefaultBattleMissionAgentSpawnLogic spawnLogic;
        private readonly MissionBattleSideSpawnContext playerSide;

        public GateBattle()
        {
            var mission = Shell<Mission>();
            mission._missionMode = MissionMode.Deployment;
            SetReadonly(mission, "GetAgentTroopClass_Override",
                (Func<BattleSideEnum, BasicCharacterObject, FormationClass>)((_, _) => FormationClass.Infantry));
            mission.Teams = new Mission.TeamCollection(mission);
            var enemyTeam = AddTeam(mission, BattleSideEnum.Defender);
            var playerTeam = AddTeam(mission, BattleSideEnum.Attacker);
            var allyTeam = AddTeam(mission, BattleSideEnum.Attacker);
            mission.Teams._playerTeam = playerTeam;
            AccessTools.Property(typeof(Mission.TeamCollection), nameof(Mission.TeamCollection.PlayerAlly))
                .SetValue(mission.Teams, allyTeam);
            Mission._current = mission;

            var deploymentPlan = new DefaultMissionDeploymentPlan(mission);
            deploymentPlan._teamDeploymentPlans.Add((enemyTeam, TeamPlan(planned: true)));
            deploymentPlan._teamDeploymentPlans.Add((playerTeam, TeamPlan(planned: true)));
            deploymentPlan._teamDeploymentPlans.Add((allyTeam, TeamPlan(planned: false)));

            spawnLogic = Shell<DefaultBattleMissionAgentSpawnLogic>();
            AccessTools.Property(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).SetValue(spawnLogic, mission);
            SetReadonly(spawnLogic, "_phases", new[]
            {
                new List<MissionSpawnPhase> { new MissionSpawnPhase { TotalSpawnNumber = 2 } },
                new List<MissionSpawnPhase> { new MissionSpawnPhase { TotalSpawnNumber = PlayerTroops, InitialSpawnNumber = PlayerTroops } },
            });
            playerSide = new MissionBattleSideSpawnContext(spawnLogic, BattleSideEnum.Attacker, null, isPlayerSide: true);
            for (int i = 0; i < PlayerTroops; i++)
                playerSide._reservedTroops.Add(new PlayerTroopOrigin());
            SetReadonly(spawnLogic, "_battleSideSpawnContexts", new[]
            {
                new MissionBattleSideSpawnContext(spawnLogic, BattleSideEnum.Defender, null, isPlayerSide: false),
                playerSide,
            });
            spawnLogic._deploymentPlan = deploymentPlan;
            spawnLogic._sidesWhereSpawnOccured = new List<BattleSideEnum>();
        }

        // Mirrors BattleDeploymentMissionController.OnSetupTeamsOfSide arming the player's side.
        public void EnablePlayerSideSpawn() => playerSide.SetSpawnTroops(true);

        public void CheckDeployment() => CheckDeploymentMethod.Invoke(spawnLogic, null);

        private static Team AddTeam(Mission mission, BattleSideEnum side)
        {
            var team = Shell<Team>();
            team._activeAgents = new MBList<Agent>();
            AccessTools.Field(typeof(Team), "<Side>k__BackingField").SetValue(team, side);
            AccessTools.Field(typeof(Team), "<Mission>k__BackingField").SetValue(team, mission);
            ((List<Team>)mission.Teams).Add(team);
            return team;
        }

        private static DefaultTeamDeploymentPlan TeamPlan(bool planned)
        {
            var initial = Shell<DefaultDeploymentPlan>();
            var reinforcement = Shell<DefaultDeploymentPlan>();
            initial.IsPlanMade = planned;
            reinforcement.IsPlanMade = planned;
            var teamPlan = Shell<DefaultTeamDeploymentPlan>();
            SetReadonly(teamPlan, "_initialPlan", initial);
            teamPlan._currentReinforcementPlan = reinforcement;
            return teamPlan;
        }
    }

    // A troop of the local player's own party, which the native team lookup routes to the player's team.
    private sealed class PlayerTroopOrigin : IAgentOriginBase
    {
        public bool IsUnderPlayersCommand => true;
        public bool IsInSameArmyAsPlayer => false;
        public uint FactionColor => 0;
        public uint FactionColor2 => 0;
        public IBattleCombatant BattleCombatant => null!;
        public int UniqueSeed => 0;
        public int Seed => 0;
        public Banner Banner => null!;
        public BasicCharacterObject Troop { get; } = Shell<BasicCharacterObject>();
        public bool HasThrownWeapon => false;
        public bool HasHeavyArmor => false;
        public bool HasShield => false;
        public bool HasSpear => false;
        public void SetWounded() { }
        public void SetKilled() { }
        public void SetRouted(bool isOrderRetreat) { }
        public void OnAgentRemoved(float agentHealth) { }
        public void OnScoreHit(BasicCharacterObject victim, BasicCharacterObject formationCaptain, int damage, bool isFatal,
            bool isTeamKill, WeaponComponentData attackerWeapon) { }
        public void SetBanner(Banner banner) { }
        public TroopTraitsMask GetTraitsMask() => default;
    }

    // Readonly engine fields are only assigned in their constructors, which the headless shells skip.
    private static void SetReadonly(object target, string field, object value) =>
        AccessTools.Field(target.GetType(), field).SetValue(target, value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T Shell<T>() where T : class
    {
        var shell = ObjectHelper.SkipConstructor<T>();
        // No native object was created, so its native finalizer must not run.
        GC.SuppressFinalize(shell);
        return shell;
    }
}
