using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using HarmonyLib;
using Missions;
using Missions.Battles;
using Missions.Hideouts;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// #3902: a joiner promoted to host after a hideout already resolved with an attacker win (duel won, defenders
/// surrendered) must not wake and charge the leftover bandits it adopts. A live fight and a field battle still do.
/// </summary>
public sealed class HideoutResolvedMigrationTests : MissionTestEnvironment
{
    public HideoutResolvedMigrationTests(ITestOutputHelper output) : base(output) => NpcReleaseMockPatches.Apply();

    [Fact]
    public void PromotedAfterADuelWin_LeavesTheSurrenderedBanditsIdle()
    {
        var (mirror, formation, hideout) = AdoptDefender(HideoutPhase.Resolved, BattleSideEnum.Attacker);

        Assert.NotEqual(AgentControllerType.AI, mirror.Controller);
        Assert.False(formation?.MovementOrderSet ?? false);
        Assert.False(formation?.IsAIControlled ?? false);
        Assert.Equal(HideoutPhase.Resolved, hideout!.Native.Phase);
    }

    // The Resolved snapshot and the promotion can land before the next tick hands the duel spectators their team back
    [Fact]
    public void PromotedAfterADuelWin_LeavesATeamlessSpectatorBanditIdle()
    {
        var (mirror, _, _) = AdoptDefender(HideoutPhase.Resolved, BattleSideEnum.Attacker, teamlessSpectator: true);

        Assert.NotEqual(AgentControllerType.AI, mirror.Controller);
    }

    [Fact]
    public void PromotedDuringALiveHideoutFight_ChargesTheAdoptedBandits()
    {
        var (mirror, formation, _) = AdoptDefender(HideoutPhase.BossBattle, BattleSideEnum.None);

        Assert.Equal(AgentControllerType.AI, mirror.Controller);
        Assert.NotNull(formation);
        Assert.True(formation!.IsAIControlled);
        Assert.True(formation.MovementOrderSet);
    }

    // The issue's case: the battle went live before the duel, so the promotion also runs the NPC release
    [Fact]
    public void PromotedAfterADuelWinInALiveBattle_LeavesTheSurrenderedBanditsIdle()
    {
        var result = AdoptDefenderInLiveBattle(HideoutPhase.Resolved, BattleSideEnum.Attacker, lateSpawn: false);

        Assert.NotEqual(AgentControllerType.AI, result.Mirror.Controller);
        Assert.True(result.Mirror.IsAiPaused, "the adoption woke a surrendered bandit");
        Assert.False(result.Formation.MovementOrderSet, "the adoption charged the surrendered bandits' formation");
        Assert.False(result.Formation.IsAIControlled, "the NPC release gave the surrendered bandits' formation to the AI");
        Assert.Equal(0, result.DefenderTeam.ResetTacticCalls);
    }

    [Fact]
    public void PromotedDuringALiveHideoutFightInALiveBattle_ReleasesAndChargesTheBandits()
    {
        var result = AdoptDefenderInLiveBattle(HideoutPhase.BossBattle, BattleSideEnum.None, lateSpawn: false);
        AssertReleasedAndCharged(result);
        Assert.Equal(1, result.DefenderTeam.ResetTacticCalls);
    }

    [Fact]
    public void PromotedInALiveFieldBattle_ReleasesAndChargesTheEnemies()
    {
        var result = AdoptDefenderInLiveBattle(phase: null, BattleSideEnum.None, lateSpawn: false);
        AssertReleasedAndCharged(result);
        Assert.Equal(1, result.DefenderTeam.ResetTacticCalls);
    }

    // A retained spawn record of the departed host drains after the migration and goes through the late adoption
    [Fact]
    public void LateSpawnedBanditAfterADuelWin_IsNotCharged()
    {
        var result = AdoptDefenderInLiveBattle(HideoutPhase.Resolved, BattleSideEnum.Attacker, lateSpawn: true);

        Assert.NotEqual(AgentControllerType.AI, result.Mirror.Controller);
        Assert.True(result.Mirror.IsAiPaused, "the late adoption woke a surrendered bandit");
        Assert.False(result.Formation.MovementOrderSet, "the late adoption charged the surrendered bandit's formation");
    }

    [Fact]
    public void LateSpawnedBanditDuringALiveHideoutFight_IsCharged()
    {
        var result = AdoptDefenderInLiveBattle(HideoutPhase.BossBattle, BattleSideEnum.None, lateSpawn: true);
        AssertReleasedAndCharged(result);
    }

    private static void AssertReleasedAndCharged(LiveAdoption result)
    {
        Assert.Equal(AgentControllerType.AI, result.Mirror.Controller);
        Assert.False(result.Mirror.IsAiPaused);
        Assert.True(result.Formation.IsAIControlled);
        Assert.True(result.Formation.MovementOrderSet);
    }

    // The old host "A" ran a defender NPC that this client mirrors as an inert puppet; A leaves and this client adopts it
    private (MirrorAgent Mirror, MockFormation? Formation, CoopHideoutMissionLogic? Hideout) AdoptDefender(
        HideoutPhase phase, BattleSideEnum winner, bool teamlessSpectator = false)
    {
        using var fixture = new MissionEngineFixture();
        var newHost = Clients.First();
        SetControllerId(newHost, "B");
        MirrorAgent? mirror = null;
        MockFormation? formation = null;
        CoopHideoutMissionLogic? hideout = null;

        newHost.Call(() =>
        {
            var mission = fixture.CreateMission(newHost);
            var controller = newHost.Resolve<CoopBattleController>();
            var registry = newHost.Resolve<INetworkAgentRegistry>();
            controller.Session.TryBegin("mapEvent1");

            hideout = CreateHideoutLogic(phase, winner);
            mission.Shell.MissionBehaviors.Add(hideout);

            var defenders = new MockTeam(BattleSideEnum.Defender);
            var spawnTeam = teamlessSpectator ? new MockTeam(BattleSideEnum.None) : defenders;
            var bandit = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None).Team(spawnTeam.Shell));
            if (teamlessSpectator)
                ((Dictionary<Agent, Team>)AccessTools.Field(typeof(CoopHideoutMissionLogic), "spectatorTeams").GetValue(hideout)!)
                    .Add(bandit, defenders.Shell);
            Assert.True(registry.TryRegisterAgent("A", Guid.NewGuid(), bandit));

            newHost.Resolve<IMessageBroker>().Publish(this, new BattleHostMigrated("mapEvent1", "A"));

            Assert.True(AgentMirror.TryGet(bandit, out mirror));
            if (mirror!.Formation != null && MockFormation.ForShell(mirror.Formation, out var assigned))
                formation = assigned;
            GC.KeepAlive(controller);
        });

        return (mirror!, formation, hideout);
    }

    private sealed record LiveAdoption(MirrorAgent Mirror, MockFormation Formation, MockTeam DefenderTeam);

    // As above, but the battle is already live (this client got the host's activation) and the AI-paused bandit
    // stands in its formation on the mission's defender team. A late spawn adopts it through ApplyLateSpawnedPuppet.
    private LiveAdoption AdoptDefenderInLiveBattle(HideoutPhase? phase, BattleSideEnum winner, bool lateSpawn)
    {
        using var fixture = new MissionEngineFixture();
        var newHost = Clients.First();
        SetControllerId(newHost, "B");
        LiveAdoption? result = null;

        newHost.Call(() =>
        {
            var mission = fixture.CreateMission(newHost);
            var controller = newHost.Resolve<CoopBattleController>();
            var registry = newHost.Resolve<INetworkAgentRegistry>();
            controller.Session.TryBegin("mapEvent1");

            if (phase.HasValue)
                mission.Shell.MissionBehaviors.Add(CreateHideoutLogic(phase.Value, winner));

            mission.AddTeam(BattleSideEnum.Attacker);
            mission.AddTeam(BattleSideEnum.Defender);
            mission.PlayerTeam = mission.AttackerTeam;

            var formation = mission.DefenderTeam.GetFormation(FormationClass.Infantry);
            var bandit = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None).Team(mission.DefenderTeam.Shell));
            bandit.Formation = formation.Shell;
            Assert.True(AgentMirror.TryGet(bandit, out var mirror));
            mirror.IsAiPaused = true;
            var banditId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("A", banditId, bandit));

            newHost.Resolve<IMessageBroker>().Publish(this, new NetworkBattleActivated("mapEvent1"));
            Assert.True(controller.Deployment.IsActivated, "precondition: the battle is live before the promotion");

            if (lateSpawn)
            {
                newHost.Resolve<IBattleHostRegistry>().Set("mapEvent1", new BattleHostAssignment("B", Array.Empty<string>()));
                var migrator = (IBattleAuthorityMigrator)AccessTools.Field(typeof(CoopBattleController), "authorityMigrator")
                    .GetValue(controller)!;
                migrator.ApplyLateSpawnedPuppet(bandit, banditId, null!, Guid.Empty);
            }
            else
            {
                newHost.Resolve<IMessageBroker>().Publish(this, new BattleHostMigrated("mapEvent1", "A"));
            }

            result = new LiveAdoption(mirror, formation, mission.DefenderTeam);
            GC.KeepAlive(controller);
        });

        return result!;
    }

    private static CoopHideoutMissionLogic CreateHideoutLogic(HideoutPhase phase, BattleSideEnum winner)
    {
        var attacker = new CoopTroopSupplier("mapEvent1", BattleSideEnum.Attacker, null, null);
        var defender = new CoopTroopSupplier("mapEvent1", BattleSideEnum.Defender, null, null);
        var logic = new CoopHideoutMissionLogic(Mock.Of<IBattleNetwork>(), Mock.Of<IMessageBroker>(),
            Mock.Of<IMissionContext>(), defender, attacker, false);
        logic.Native = new FixedPhaseController(phase);
        AccessTools.Field(typeof(CoopHideoutMissionLogic), "winner").SetValue(logic, winner);
        return logic;
    }

    private sealed class FixedPhaseController : ICoopHideoutNativeController
    {
        public FixedPhaseController(HideoutPhase phase) => Phase = phase;
        public HideoutPhase Phase { get; private set; }
        public Agent Boss => null!;
        public void Initialize(bool authority, bool migration) { }
        public void TickAuthority(float dt) { }
        public void Apply(HideoutPhase phase, Agent boss, int initialPopulation) => Phase = phase;
        public void ResumeBossBattle() { }
    }
}
