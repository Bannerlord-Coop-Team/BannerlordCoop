using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents.TroopSupply;
using HarmonyLib;
using Missions;
using Missions.Battles;
using Missions.Hideouts;
using Missions.Services.Network;
using Moq;
using SandBox.Missions.MissionLogics.Hideout;
using SandBox.Missions.MissionLogics;
using SandBox.Objects.AreaMarkers;
using SandBox.Objects.Usables;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class HideoutMissionRuntimeTests : MissionTestEnvironment
{
    public HideoutMissionRuntimeTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void JoiningPlayer_CanOrderOwnTroopsWithoutTakingCommandOfAnotherPlayersFormation()
    {
        using var fixture = new MissionEngineFixture();
        var harmony = new Harmony($"hideout-orders.{Guid.NewGuid()}");
        harmony.Patch(AccessTools.Method(typeof(Formation), nameof(Formation.GetCountOfUnitsWithCondition)),
            prefix: new HarmonyMethod(typeof(HideoutMissionRuntimeTests), nameof(CountFormationUnits)));
        try
        {
            var client = Clients.Last();
            client.Call(() =>
            {
                var mission = fixture.CreateMission(client);
                var team = mission.AddTeam(BattleSideEnum.Attacker);
                var allyTeam = mission.AddTeam(BattleSideEnum.Attacker);
                mission.PlayerTeam = mission.AttackerTeam;
                var hero = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(team).Controller(AgentControllerType.Player));
                var ally = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(allyTeam).Controller(AgentControllerType.None));
                mission.MainAgent = hero;
                var ownFormation = team.GetFormation(FormationClass.Infantry);
                var allyFormation = allyTeam.GetFormation(FormationClass.Infantry);
                ally.Formation = allyFormation;
                allyFormation.PlayerOwner = ally;
                for (var index = 0; index < 3; index++)
                    mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(team).Controller(AgentControllerType.AI))
                        .Formation = ownFormation;
                var orders = ObjectHelper.SkipConstructor<OrderController>();
                orders.Owner = hero;
                Assert.False(orders.IsFormationSelectable(ownFormation));
                var logic = new CoopHideoutMissionLogic(Mock.Of<IBattleNetwork>(), Mock.Of<IMessageBroker>(),
                    Mock.Of<IMissionContext>(), null, null, true);
                AccessTools.Property(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).SetValue(logic, mission.Shell);
                try
                {
                    logic.AssignTroopCommand();

                    Assert.Same(hero, ownFormation.PlayerOwner);
                    Assert.True(orders.IsFormationSelectable(ownFormation));
                    Assert.Equal(3, ownFormation.GetCountOfUnitsWithCondition(agent => agent.Health > 0));
                    Assert.False(orders.IsFormationSelectable(allyFormation));
                    Assert.Same(ally, allyFormation.PlayerOwner);
                    Assert.True(MockFormation.ForShell(ownFormation, out var own));
                    Assert.False(own.IsAIControlled);
                }
                finally { logic.OnRemoveBehavior(); }
            });
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static bool CountFormationUnits(Formation __instance, Func<Agent, bool> function, ref int __result)
    {
        __result = Mission.Current.Agents.Count(agent => agent.Formation == __instance && function(agent));
        return false;
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void JoiningHero_NativeSpawnAcceptsBothAnAlliedPositionAndTheDefaultEntrance(bool direct, bool hasAlly)
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.Last();
        client.Call(() =>
        {
            var mission = fixture.CreateMission(client);
            var team = mission.AddTeam(BattleSideEnum.Attacker);
            mission.PlayerTeam = mission.AttackerTeam;
            if (hasAlly)
                mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(team)
                    .Controller(AgentControllerType.None).InitialPosition(new Vec3(20f, 30f, 2f))
                    .InitialDirection(new Vec2(0f, 1f)));
            var attacker = new CoopTroopSupplier("raid", BattleSideEnum.Attacker, null, null);
            var defender = new CoopTroopSupplier("raid", BattleSideEnum.Defender, null, null);
            var logic = new CoopHideoutMissionLogic(Mock.Of<IBattleNetwork>(), Mock.Of<IMessageBroker>(),
                Mock.Of<IMissionContext>(), defender, attacker, direct);
            AccessTools.Property(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).SetValue(logic, mission.Shell);
            logic.Native = new PromotionController(() => { });
            logic.Native.Apply(HideoutPhase.Camp, null, 25);
            var origin = Mock.Of<IAgentOriginBase>(value => value.Troop == Game.Current.PlayerTroop &&
                value.UniqueSeed == 101 && value.IsUnderPlayersCommand);
            try
            {
                // Run the real SpawnTroop; only engine agent creation and visual calls are replaced.
                logic.SpawnOrigins(new[] { origin }, returningHero: true);

                var hero = Assert.Single(mission.Agents, agent => agent.Origin == origin);
                Assert.Equal(AgentControllerType.Player, hero.Controller);
                Assert.Equal(hasAlly ? new Vec3(21f, 31f, 2f) : default, hero.Position);
                Assert.Equal(hasAlly ? new Vec2(0f, 1f) : default, hero.GetMovementDirection());
            }
            finally { logic.OnRemoveBehavior(); }
        }, new[]
        {
            AccessTools.Method(typeof(Agent), nameof(Agent.GetAgentFlags)),
            AccessTools.Method(typeof(Agent), nameof(Agent.SetAgentFlags)),
            AccessTools.Method(typeof(Agent), nameof(Agent.SetWatchState)),
            AccessTools.Method(typeof(Agent), nameof(Agent.WieldInitialWeapons)),
        });
    }

    [Fact]
    public void AmbushReady_WaitsForThePromotedHostsExpandedDefenderReserve()
    {
        var attacker = new CoopTroopSupplier("raid", BattleSideEnum.Attacker, null, null);
        var defender = new CoopTroopSupplier("raid", BattleSideEnum.Defender, null, null);
        attacker.SetReserve(Array.Empty<PartyReserve>(), 1, 1, 1000);
        Assert.False(CoopHideoutMissionLogic.ReservesReady(true, attacker, defender));
        defender.SetReserve(Array.Empty<PartyReserve>(), 25, 0, 1000);
        Assert.True(CoopHideoutMissionLogic.ReservesReady(false, attacker, defender));
        Assert.False(CoopHideoutMissionLogic.ReservesReady(true, attacker, defender));
        defender.SetReserve(new[] { new PartyReserve("defenders", 0,
            Enumerable.Range(1, 25).Select(seed => new TroopReserveEntry(seed, "guard", 0)).ToArray()) }, 25, 0, 1000);
        Assert.True(CoopHideoutMissionLogic.ReservesReady(true, attacker, defender));
    }

    [Fact]
    public void NativeAmbushAfterStart_DoesNotConsumeOrSpawnBeforeMissionReadiness()
    {
        var attacker = new CoopTroopSupplier("raid", BattleSideEnum.Attacker, null, null);
        var defender = new CoopTroopSupplier("raid", BattleSideEnum.Defender, null, null);
        var logic = new CoopHideoutMissionLogic(Mock.Of<IBattleNetwork>(), Mock.Of<IMessageBroker>(),
            Mock.Of<IMissionContext>(), defender, attacker, false);
        var native = new CoopHideoutAmbushController(logic,
            new IMissionTroopSupplier[] { logic.Defender, logic.Attacker });
        logic.Native = native;
        native.AfterStart();
        native.OnMissionTick(1f);
        Assert.Equal(HideoutPhase.Waiting, native.Phase);
        Assert.False(native._isMissionInitialized);
        Assert.False(attacker.IsPopulated);
        Assert.False(defender.IsPopulated);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CampLeaderFalls_SurvivingRemoteHeroKeepsTheAttackerSideAlive(bool direct)
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var attackerTeam = mock.AddTeam(BattleSideEnum.Attacker);
            mock.PlayerTeam = mock.AttackerTeam;
            var allyTeam = mock.AddTeam(BattleSideEnum.Attacker);
            var defenderTeam = mock.AddTeam(BattleSideEnum.Defender);
            var attacker = new CoopTroopSupplier("raid", BattleSideEnum.Attacker, null, null);
            var defender = new CoopTroopSupplier("raid", BattleSideEnum.Defender, null, null);
            var logic = new CoopHideoutMissionLogic(Mock.Of<IBattleNetwork>(), client.Resolve<IMessageBroker>(),
                Mock.Of<IMissionContext>(), defender, attacker, direct);
            AccessTools.Property(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).SetValue(logic, mock.Shell);
            AccessTools.Field(typeof(CoopHideoutMissionLogic), "initialized").SetValue(logic, true);
            var suppliers = new IMissionTroopSupplier[] { logic.Defender, logic.Attacker };
            var native = direct ? (MissionLogic)new CoopHideoutAssaultController(logic, suppliers)
                : new CoopHideoutAmbushController(logic, suppliers);
            AccessTools.Property(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).SetValue(native, mock.Shell);
            logic.Native = (ICoopHideoutNativeController)native;
            logic.Native.Apply(HideoutPhase.Camp, null, 25);

            var leader = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(attackerTeam).Controller(AgentControllerType.Player));
            var joiningHero = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(allyTeam).Controller(AgentControllerType.None));
            var guard = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(defenderTeam).Controller(AgentControllerType.AI));
            mock.MainAgent = leader;
            logic.OnAgentBuild(leader, null);
            logic.OnAgentBuild(joiningHero, null);
            logic.OnAgentBuild(guard, null);
            Assert.True(AgentMirror.TryGet(leader, out var fallen));
            fallen.IsActive = false;

            native.OnAgentRemoved(leader, guard, AgentState.Unconscious, default);
            Assert.False(logic.IsSideDepleted(BattleSideEnum.Attacker));
            Assert.False(logic.IsSideDepleted(BattleSideEnum.Defender));
            logic.WithLivingLeader(() => Assert.Same(joiningHero, mock.MainAgent));
            Assert.Same(leader, mock.MainAgent);

            logic.Native.Apply(HideoutPhase.Duel, guard, 25);
            Assert.True(logic.IsSideDepleted(BattleSideEnum.Attacker));
            Assert.False(logic.ShouldResumeBossBattle(HideoutPhase.Duel));
            if (!direct)
                AssertNativeBattleEndUsesSpawnLogic(Assert.IsAssignableFrom<IMissionAgentSpawnLogic>(native), mock.Shell);
            logic.Native.Apply(HideoutPhase.Camp, guard, 25);
            Assert.True(AgentMirror.TryGet(joiningHero, out var lastPlayer));
            lastPlayer.IsActive = false;
            Assert.True(logic.IsSideDepleted(BattleSideEnum.Attacker));
            logic.OnRemoveBehavior();
        });
    }

    [Fact]
    public void StagedBossReserve_SurvivesMigrationWithoutRespawningCampCasualties()
    {
        var (mapEventId, _) = SetupCoopBattle("first", "enemy");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var battle));
            var party = battle.DefenderSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(party, out var partyId));
            var guard = Server.CreateRegisteredObject<CharacterObject>("hideout_guard");
            Assert.True(Server.ObjectManager.TryGetId(guard, out var guardId));
            var entries = Enumerable.Range(1, 5).Select(seed => new TroopReserveEntry(seed, guardId, 0, seed)).ToArray();
            var source = new CoopTroopSupplier(mapEventId, BattleSideEnum.Defender, Server.ObjectManager, null);
            source.SetReserve(new[] { new PartyReserve(partyId, 0, entries) }, 5, 0, 1000);
            var ledger = new HideoutStateLedger();
            var staged = new HideoutTroopSupplier(source, ledger);
            staged.Refresh(false);
            Assert.Equal(5, source.GetSuppliedByParty().Single().supplied);
            var camp = staged.SupplyTroops(3).ToArray();
            Assert.Equal(new[] { 1, 2, 3 }, camp.Select(origin => origin.UniqueSeed));
            foreach (var origin in camp) ledger.RecordSpawn(origin.UniqueSeed);

            var successorSource = new CoopTroopSupplier(mapEventId, BattleSideEnum.Defender, Server.ObjectManager, null);
            successorSource.SetReserve(new[] { new PartyReserve(partyId, 5, entries) }, 5, 0, 1000);
            var successor = new HideoutTroopSupplier(successorSource, ledger);
            successor.ResetForMigration();
            Assert.Equal(new[] { 4, 5 }, successor.SupplyTroops(20).Select(origin => origin.UniqueSeed));
            successor.Refresh(false);
            Assert.Empty(successor.SupplyTroops(20));
            Assert.Equal(5, successorSource.GetSuppliedByParty().Single().supplied);
        });
    }

    [Fact]
    public void ReturningHero_UsesItsConsumedDescriptorWithoutRefillingEscorts()
    {
        var (mapEventId, _) = SetupCoopBattle("returning", "enemy");
        RunWithSceneShims(() => Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var battle));
            var party = battle.AttackerSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(party, out var partyId));
            var hero = party.Party.LeaderHero.CharacterObject;
            Assert.True(Server.ObjectManager.TryGetId(hero, out var heroId));
            var entries = new[] { new TroopReserveEntry(1, heroId, 0) };
            var source = new CoopTroopSupplier(mapEventId, BattleSideEnum.Attacker, Server.ObjectManager, null);
            source.SetReserve(new[] { new PartyReserve(partyId, 1, entries) }, 1, 1, 1000);
            var ledger = new HideoutStateLedger();
            ledger.RecordSpawn(1);
            var supplied = new HideoutTroopSupplier(source, ledger);
            supplied.Refresh(false);
            Assert.Null(supplied.TakeHero(hero));
            Assert.Equal(1, supplied.TakeHero(hero, returningHero: true).UniqueSeed);
            Assert.True(ledger.WasSpawned(1));
            Assert.Empty(supplied.SupplyTroops(14));
            Assert.Equal(1, source.GetSuppliedByParty().Single().supplied);
        }));
    }

    [Fact]
    public void NativeAmbushBossPadding_NeverClonesOutsideTheAuthoritativeReserve()
    {
        var attacker = new CoopTroopSupplier("raid", BattleSideEnum.Attacker, null, null);
        var defender = new CoopTroopSupplier("raid", BattleSideEnum.Defender, null, null);
        var logic = new CoopHideoutMissionLogic(Mock.Of<IBattleNetwork>(), Mock.Of<IMessageBroker>(),
            Mock.Of<IMissionContext>(), defender, attacker, false);
        var native = new CoopHideoutAmbushController(logic, new IMissionTroopSupplier[] { logic.Defender, logic.Attacker });
        native._allEnemyTroops = new List<IAgentOriginBase> { Mock.Of<IAgentOriginBase>(), Mock.Of<IAgentOriginBase>() };
        int requested = 4;
        HideoutBossReservePatch.Prefix(native, ref requested);
        Assert.Equal(2, requested);
        native._allEnemyTroops.Clear();
        HideoutBossReservePatch.Prefix(native, ref requested);
        Assert.Equal(0, requested);
        logic.OnRemoveBehavior();
    }

    [Fact]
    public void NativeAreaUse_DoesNotInvokeTheHostOnlyReinforcementDelegateOnAPeer()
    {
        RunWithSceneShims(() =>
        {
            var area = new CoopHideoutStealthAreaLogic();
            area.SpawnReinforcementAllyTroopsEvent += (_, _) => throw new InvalidOperationException("Uninitialized native supplier used");
            area.OnObjectUsed(null, ObjectHelper.SkipConstructor<StealthAreaUsePoint>());
        });
    }

    [Fact]
    public void NativeAreaSentryRemoval_AcceptsAKillWithoutTheLocalMainHero()
    {
        RunWithSceneShims(() =>
        {
            var areaLogic = new CoopHideoutStealthAreaLogic();
            var area = ObjectHelper.SkipConstructor<StealthAreaMissionLogic.StealthAreaData>();
            var marker = ObjectHelper.SkipConstructor<StealthAreaMarker>();
            var killed = ObjectHelper.SkipConstructor<Agent>();
            var surviving = ObjectHelper.SkipConstructor<Agent>();
            AccessTools.Field(typeof(StealthAreaMissionLogic.StealthAreaData), nameof(area.StealthAreaMarkers))
                .SetValue(area, new Dictionary<StealthAreaMarker, List<Agent>> { [marker] = new() { killed, surviving } });
            areaLogic._stealthAreaData.Add(area);
            areaLogic.OnAgentRemoved(killed, null, AgentState.Killed, default);
            Assert.Equal(new[] { surviving }, area.StealthAreaMarkers[marker]);
        });
    }

    [Fact]
    public void PromotionDuringDuel_RestoresOtherPlayersBeforeResumingTheGroupFight()
    {
        using var fixture = new MissionEngineFixture();
        RunWithSceneShims(() => Clients.First().Call(() =>
        {
            var mission = fixture.CreateMission(Clients.First());
            var team = mission.AddTeam(BattleSideEnum.Attacker);
            var player = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(Team.Invalid).Controller(AgentControllerType.None));
            mission.MainAgent = player;
            var attacker = new CoopTroopSupplier("raid", BattleSideEnum.Attacker, null, null);
            var defender = new CoopTroopSupplier("raid", BattleSideEnum.Defender, null, null);
            attacker.SetReserve(Array.Empty<PartyReserve>(), 0, 0, 1000);
            defender.SetReserve(Array.Empty<PartyReserve>(), 0, 0, 1000);
            var logic = new CoopHideoutMissionLogic(Mock.Of<IBattleNetwork>(), Mock.Of<IMessageBroker>(),
                Mock.Of<IMissionContext>(), defender, attacker, false);
            AccessTools.Property(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).SetValue(logic, mission.Shell);
            var battle = ObjectHelper.SkipConstructor<CoopBattleController>();
            AccessTools.Field(typeof(CoopBattleController), "<Session>k__BackingField").SetValue(battle,
                Mock.Of<IBattleSession>(value => value.HasInstance && value.IsLocalHost && value.HostEpoch == 2 && value.InstanceId == "raid"));
            AccessTools.Field(typeof(CoopHideoutMissionLogic), "battle").SetValue(logic, battle);
            AccessTools.Field(typeof(CoopHideoutMissionLogic), "initialized").SetValue(logic, true);
            AccessTools.Field(typeof(CoopHideoutMissionLogic), "deployed").SetValue(logic, true);
            AccessTools.Field(typeof(CoopHideoutMissionLogic), "heroEntered").SetValue(logic, true);
            AccessTools.Field(typeof(CoopHideoutMissionLogic), "authorityEpoch").SetValue(logic, 1);
            AccessTools.Field(typeof(CoopHideoutMissionLogic), "lastPhase").SetValue(logic, HideoutPhase.BossBattle);
            AccessTools.Field(typeof(CoopHideoutMissionLogic), "snapshotTimer").SetValue(logic, 100f);
            var teams = (Dictionary<Agent, Team>)AccessTools.Field(typeof(CoopHideoutMissionLogic), "spectatorTeams").GetValue(logic);
            var controllers = (Dictionary<Agent, AgentControllerType>)AccessTools.Field(typeof(CoopHideoutMissionLogic), "spectatorControllers").GetValue(logic);
            teams.Add(player, team);
            controllers.Add(player, AgentControllerType.Player);
            var native = new PromotionController(() =>
            {
                Assert.Same(team, player.Team);
                Assert.Equal(AgentControllerType.Player, player.Controller);
            });
            logic.Native = native;
            logic.OnMissionTick(0.1f);
            Assert.True(native.Resumed);
            Assert.Empty(teams);
            Assert.Empty(controllers);
            logic.OnRemoveBehavior();
        }));
    }

    private sealed class PromotionController : ICoopHideoutNativeController
    {
        private readonly Action onResume;
        public PromotionController(Action onResume) => this.onResume = onResume;
        public HideoutPhase Phase { get; private set; } = HideoutPhase.Duel;
        public Agent Boss => null;
        public bool Resumed { get; private set; }
        public void Initialize(bool authority, bool migration) { }
        public void TickAuthority(float dt) { }
        public void Apply(HideoutPhase phase, Agent boss, int initialPopulation) => Phase = phase;
        public void ResumeBossBattle()
        {
            onResume();
            Resumed = true;
            Phase = HideoutPhase.BossBattle;
        }
    }

    private static void RunWithSceneShims(Action action)
    {
        var harmony = new Harmony($"hideout-runtime.{Guid.NewGuid()}");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents"),
                prefix: new HarmonyMethod(typeof(HideoutMissionRuntimeTests), nameof(SkipScriptCache)));
            harmony.Patch(AccessTools.PropertyGetter(typeof(PartyBase), nameof(PartyBase.Name)),
                prefix: new HarmonyMethod(typeof(HideoutMissionRuntimeTests), nameof(PartyName)));
            harmony.Patch(AccessTools.Method(typeof(Agent), nameof(Agent.SetTeam)),
                prefix: new HarmonyMethod(typeof(HideoutMissionRuntimeTests), nameof(SetTeam)));
            action();
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static bool SkipScriptCache() => false;

    private static IMissionAgentSpawnLogic activeSpawnLogic;
    private static void AssertNativeBattleEndUsesSpawnLogic(IMissionAgentSpawnLogic spawnLogic, Mission mission)
    {
        var harmony = new Harmony($"hideout-end-check.{Guid.NewGuid()}");
        try
        {
            activeSpawnLogic = spawnLogic;
            harmony.Patch(AccessTools.Method(typeof(Mission), nameof(Mission.GetMissionBehavior)).MakeGenericMethod(typeof(IMissionAgentSpawnLogic)),
                prefix: new HarmonyMethod(typeof(HideoutMissionRuntimeTests), nameof(FindSpawnLogic)) { priority = Priority.First });
            harmony.Patch(AccessTools.Method(typeof(MBCommon), nameof(MBCommon.GetTotalMissionTime)),
                prefix: new HarmonyMethod(typeof(HideoutMissionRuntimeTests), nameof(FixedMissionTime)));
            var end = new BattleEndLogic();
            AccessTools.Property(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).SetValue(end, mission);
            end.OnBehaviorInitialize();
            Assert.Same(spawnLogic, end._missionAgentSpawnLogic);
            end._canCheckForEndConditionSiege = true;
            end.ChangeCanCheckForEndCondition(true);
            end.CheckIsEnemySideRetreatingOrOneSideDepleted();
            Assert.True(end._isPlayerSideDepleted);
            Assert.False(end._isEnemySideDepleted);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            activeSpawnLogic = null;
        }
    }

    private static bool FindSpawnLogic(ref object __result)
    {
        __result = activeSpawnLogic;
        return false;
    }

    private static bool FixedMissionTime(ref float __result)
    {
        __result = 0f;
        return false;
    }
    private static bool SetTeam(Agent __instance, Team team)
    {
        if (!AgentMirror.TryGet(__instance, out var mirror)) return true;
        mirror.Team = team;
        return false;
    }
    private static bool PartyName(ref TextObject __result)
    {
        __result = new TextObject("Hideout test party");
        return false;
    }

    [Theory]
    [InlineData((int)HideoutPhase.Stealth, false)]
    [InlineData((int)HideoutPhase.Camp, true)]
    [InlineData((int)HideoutPhase.CallingTroops, true)]
    [InlineData((int)HideoutPhase.Duel, true)]
    [InlineData((int)HideoutPhase.Resolved, false)]
    public void LateAmbushJoin_SpawnsEscortsOnlyWhenTheNativeFightHasCalledThem(int phase, bool expected)
        => Assert.Equal(expected, CoopHideoutMissionLogic.ShouldSpawnEscorts(false, (HideoutPhase)phase));

    [Fact]
    public void PhaseReplay_RejectsOldHostAndRetainsSpawnIdentityAcrossMigration()
    {
        var ledger = new HideoutStateLedger();
        var initial = State("first", 1, 3, HideoutPhase.Camp, 10, 11);
        Assert.True(ledger.TryAccept(initial, "raid", "first", 1));
        Assert.False(ledger.TryAccept(State("other", 1, 4, HideoutPhase.Resolved), "raid", "first", 1));
        Assert.False(ledger.TryAccept(State("first", 1, 2, HideoutPhase.Resolved), "raid", "first", 1));
        Assert.True(ledger.TryAccept(State("second", 2, 1, HideoutPhase.BossBattle, 12), "raid", "second", 2));
        Assert.False(ledger.TryAccept(State("first", 1, 100, HideoutPhase.Resolved), "raid", "second", 2));
        Assert.Equal(new[] { 10, 11, 12 }, ledger.GetSpawnedSeeds());
        Assert.Equal(HideoutPhase.BossBattle, ledger.Latest.Phase);
    }

    private static NetworkHideoutState State(string host, int epoch, long revision, HideoutPhase phase, params int[] seeds)
        => new("raid", host, epoch, revision, phase, 0, 0, seeds, BattleSideEnum.None, 25);
}
