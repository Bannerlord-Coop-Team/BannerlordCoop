using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Hideouts.Messages;
using GameInterface.Services.Players;
using HarmonyLib;
using Missions.Battles;
using Missions.Hideouts;
using Moq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions.MissionLogics;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class HideoutRogueryXpTests : MissionTestEnvironment
{
    private const float SucceededXp = 800f;
    private const float FailedXp = 300f;
    private const float GhostXp = 1200f;

    public HideoutRogueryXpTests(ITestOutputHelper output) : base(output)
    {
        // The headless environment omits join-time registration of pre-existing skills.
        uint rogueryHandle = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.AddExisting("SkillObject_roguery", DefaultSkills.Roguery));
            Assert.True(Server.ObjectManager.TryGetHandle(DefaultSkills.Roguery, out rogueryHandle));
        });
        foreach (var client in Clients)
            client.Call(() => Assert.True(client.ObjectManager.AddExisting(
                "SkillObject_roguery", DefaultSkills.Roguery, rogueryHandle)));
    }

    private static bool FixedMissionEndXp(bool isSucceeded, ref float __result)
    {
        __result = isSucceeded ? SucceededXp : FailedXp;
        return false;
    }

    private static bool FixedGhostXp(ref float __result)
    {
        __result = GhostXp;
        return false;
    }

    [Fact]
    public void BattleHostWinsTheRaid_EachRaidingPlayerHeroGetsTheRogueryXpOnce()
    {
        AssertEachRaiderGetsRogueryXpOnce(HideoutRogueryXpReward.MissionSucceeded, SucceededXp, (host, fixture) =>
        {
            var mock = fixture.CreateMission(host);
            var (logic, nativeLogic, attackerTeam, defenderTeam) =
                CreateHideout(mock, host.Resolve<IMessageBroker>(), direct: false, HideoutPhase.BossBattle);
            var native = (CoopHideoutAmbushController)nativeLogic;
            native._isMissionInitialized = true;
            native._troopsInitialized = true;
            native._playerPriorTroops = new List<IAgentOriginBase>();
            native._battleEndLogic = new BattleEndLogic { Mission = mock.Shell, _missionAgentSpawnLogic = Mock.Of<IMissionAgentSpawnLogic>() };
            native._missionObjectiveLogic = ObjectHelper.SkipConstructor<MissionObjectiveLogic>();
            var leader = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(attackerTeam).Controller(AgentControllerType.Player));
            var boss = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(defenderTeam).Controller(AgentControllerType.AI));
            mock.MainAgent = leader;
            logic.OnAgentBuild(leader, null);
            logic.OnAgentBuild(boss, null);
            Assert.True(AgentMirror.TryGet(boss, out var fallenBoss));
            fallenBoss.IsActive = false;
            // The mock engine's enemy team is always the attackers; native checks the bandits through it here.
            var enemyTeam = new Harmony($"hideout-roguery-xp-enemy.{Guid.NewGuid()}");
            enemyTeam.Patch(AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.PlayerEnemyTeam)),
                prefix: new HarmonyMethod(typeof(HideoutRogueryXpTests), nameof(DefendersAsPlayerEnemy)) { priority = Priority.First });
            try
            {
                // The native boss fight resolves on the battle host's tick, which keeps running after it.
                native.TickAuthority(0.1f);
                native.TickAuthority(0.1f);
                Assert.Equal(HideoutPhase.Resolved, native.Phase);
            }
            finally
            {
                enemyTeam.UnpatchAll(enemyTeam.Id);
                logic.OnRemoveBehavior();
            }
        });
    }

    private static bool DefendersAsPlayerEnemy(Mission __instance, ref Team __result)
    {
        if (!MockMission.ForShell(__instance, out var mock)) return true;
        __result = mock.DefenderTeam.Shell;
        return false;
    }

    [Fact]
    public void BattleHostSpottedInStealth_EachRaidingPlayerHeroGetsTheFailedRogueryXpOnce()
    {
        AssertEachRaiderGetsRogueryXpOnce(HideoutRogueryXpReward.MissionFailed, FailedXp, (host, fixture) =>
        {
            var mock = fixture.CreateMission(host);
            var (logic, _, _, _) = CreateHideout(mock, host.Resolve<IMessageBroker>(), direct: false, HideoutPhase.Stealth);
            var battle = AttachBattle(logic,
                Mock.Of<IBattleSession>(value => value.HasInstance && value.IsLocalHost && value.HostEpoch == 1 && value.InstanceId == "raid"));
            AttachBattleEndLogic(mock, battle);
            // The resolved phase snapshot reads the defenders still standing.
            mock.DefenderTeam.Shell._activeAgents = new MBList<Agent>();
            try
            {
                // Both the fail counter and its popup end the raid, as they do natively.
                logic.OnStealthFailure();
                logic.OnStealthFailure();
                Assert.Equal(HideoutPhase.Resolved, logic.Native.Phase);
            }
            finally { logic.OnRemoveBehavior(); }
        });
    }

    [Fact]
    public void NativeGhostBonusOnTheBattleHost_EachRaidingPlayerHeroGetsItOnceAndIsSentTheBonus()
    {
        // Native HideoutAmbushMissionController.OnObjectUsed gives it when the signal fire is lit unseen.
        AssertEachRaiderGetsRogueryXpOnce(HideoutRogueryXpReward.ClearedAsGhost, GhostXp,
            (_, _) => Campaign.Current.SkillLevelingManager.OnHideoutClearedAsGhost());
    }

    private void AssertEachRaiderGetsRogueryXpOnce(
        HideoutRogueryXpReward reward,
        float rolledXp,
        Action<EnvironmentInstance, MissionEngineFixture> raidOnBattleHost)
    {
        // The battle helper needs a second side, so "bandits" defends; the host and "joiner" raid.
        var (mapEventId, partyIds, heroIds) = SetupCoopBattleWithHeroes("host", "bandits", "joiner");
        var (hostHeroId, defenderHeroId, joinerHeroId) = (heroIds[0], heroIds[1], heroIds[2]);
        var hostPartyId = partyIds[0];
        var raiderIds = new[] { hostHeroId, joinerHeroId };
        var host = Clients.First();
        var defenderClient = Clients.Last();
        var before = new Dictionary<string, float>();
        var gain = new Dictionary<string, float>();
        float defenderBefore = 0;
        Server.Call(() =>
        {
            var players = Server.Resolve<IPlayerManager>();
            players.SetPeer("host", host.NetPeer);
            players.SetPeer("bandits", defenderClient.NetPeer);
            defenderBefore = Server.GetRegisteredObject<Hero>(defenderHeroId).HeroDeveloper.GetSkillXp(DefaultSkills.Roguery);
            foreach (var heroId in raiderIds)
            {
                var hero = Server.GetRegisteredObject<Hero>(heroId);
                before[heroId] = hero.HeroDeveloper.GetSkillXp(DefaultSkills.Roguery);
                gain[heroId] = rolledXp
                    * Campaign.Current.Models.GenericXpModel.GetXpMultiplier(hero)
                    * hero.HeroDeveloper.GetFocusFactor(DefaultSkills.Roguery);
            }
        });

        // The model rolls a random amount, so fix it to check the exact xp each hero gets.
        var harmony = new Harmony($"hideout-roguery-xp.{Guid.NewGuid()}");
        harmony.Patch(AccessTools.Method(typeof(DefaultHideoutModel), nameof(DefaultHideoutModel.GetRogueryXpGainOnHideoutMissionEnd)),
            prefix: new HarmonyMethod(typeof(HideoutRogueryXpTests), nameof(FixedMissionEndXp)));
        harmony.Patch(AccessTools.Method(typeof(DefaultHideoutModel), nameof(DefaultHideoutModel.GetRogueryXpGainAsGhost)),
            prefix: new HarmonyMethod(typeof(HideoutRogueryXpTests), nameof(FixedGhostXp)));
        try
        {
            using var fixture = new MissionEngineFixture();
            host.Call(() =>
            {
                var previousMainParty = Campaign.Current.MainParty;
                Campaign.Current.MainParty = host.GetRegisteredObject<MobileParty>(hostPartyId);
                try
                {
                    Assert.NotNull(MapEvent.PlayerMapEvent);
                    raidOnBattleHost(host, fixture);
                }
                finally { Campaign.Current.MainParty = previousMainParty; }
            }, new MethodBase[] { AccessTools.Method(typeof(MapEvent), nameof(MapEvent.SetOverrideWinner)) });
        }
        finally { harmony.UnpatchAll(harmony.Id); }

        var sent = Assert.Single(host.NetworkSentMessages.GetMessages<NetworkHideoutRogueryXpEarned>());
        Assert.Equal(mapEventId, sent.MapEventId);
        Assert.Equal(reward, sent.Reward);
        var after = new Dictionary<string, float>();
        float defenderAfter = 0;
        Server.Call(() =>
        {
            defenderAfter = Server.GetRegisteredObject<Hero>(defenderHeroId).HeroDeveloper.GetSkillXp(DefaultSkills.Roguery);
            foreach (var heroId in raiderIds)
                after[heroId] = Server.GetRegisteredObject<Hero>(heroId).HeroDeveloper.GetSkillXp(DefaultSkills.Roguery);
        });
        Assert.Equal(defenderBefore, defenderAfter);
        foreach (var heroId in raiderIds)
        {
            Assert.True(gain[heroId] > 0);
            Assert.Equal(before[heroId] + gain[heroId], after[heroId]);
        }

        // Only the ghost bonus is announced, and only to the raider it went to; the joiner has no client here.
        var hostBonuses = host.InternalMessages.GetMessages<NetworkHideoutGhostBonusGained>();
        if (reward == HideoutRogueryXpReward.ClearedAsGhost)
        {
            var bonus = Assert.Single(hostBonuses);
            Assert.Equal(after[hostHeroId] - before[hostHeroId], bonus.Xp);
            Assert.Equal(rolledXp, bonus.BaseXp);
        }
        else
        {
            Assert.Empty(hostBonuses);
        }
        Assert.Empty(defenderClient.InternalMessages.GetMessages<NetworkHideoutGhostBonusGained>());

        FlushCoalescer();
        foreach (var client in Clients)
            client.Call(() =>
            {
                foreach (var heroId in raiderIds)
                    Assert.Equal(after[heroId], client.GetRegisteredObject<Hero>(heroId).HeroDeveloper.GetSkillXp(DefaultSkills.Roguery));
            });
    }
}
