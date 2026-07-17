using Common;
using E2E.Tests.Environment.MockEngine;
using HarmonyLib;
using Missions;
using Missions.Agents.Patches;
using Missions.Tournaments;
using Missions.Tournaments.Messages;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Tournaments;

public class TournamentDamageReplayTests : MissionTestEnvironment
{
    public TournamentDamageReplayTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ReplicatedDamage_AppliesToRemoteVictimCopy()
    {
        using var fixture = new MissionEngineFixture();
        var harmony = new Harmony("e2e.tournamentdamage.registerblow");
        MethodInfo registerBlow = AccessTools.Method(
            typeof(Agent),
            nameof(Agent.RegisterBlow),
            new[] { typeof(Blow), typeof(AttackCollisionData).MakeByRefType() });
        MethodInfo registerBlowPrefix = AccessTools.Method(typeof(RegisterBlowPatch), "Prefix");
        Assert.NotNull(registerBlow);
        Assert.NotNull(registerBlowPrefix);
        harmony.Patch(
            registerBlow,
            prefix: new HarmonyMethod(registerBlowPrefix) { priority = Priority.First });

        try
        {
            var observer = Clients.First();
            SetControllerId(observer, "observer");

            observer.Call(() =>
            {
                var mock = fixture.CreateMission(observer);
                var controller = observer.Resolve<CoopTournamentController>();
                var registry = observer.Resolve<INetworkAgentRegistry>();
                Agent victim = mock.SpawnAgent(
                    new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
                Agent attacker = mock.SpawnAgent(
                    new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
                Guid victimId = Guid.NewGuid();
                Guid attackerId = Guid.NewGuid();

                Assert.True(registry.TryRegisterAgent("victim-owner", victimId, victim));
                Assert.True(registry.TryRegisterAgent("attacker-owner", attackerId, attacker));
                Assert.False(registry.IsLocallyControlled(victim));

                var message = new NetworkApplyTournamentDamage(
                    "session",
                    "match",
                    1,
                    "attacker-owner",
                    1,
                    victimId,
                    attackerId,
                    new Blow(attacker.Index) { InflictedDamage = 30, DamageType = DamageTypes.Cut },
                    default);
                MethodInfo applyTournamentDamage = typeof(CoopTournamentController).GetMethod(
                    "ApplyTournamentDamage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(applyTournamentDamage);

                GameThread.Run(() => applyTournamentDamage.Invoke(controller, new object[] { message }), true);

                Assert.True(AgentMirror.TryGet(victim, out var mirror));
                Assert.Equal(70f, mirror.Health);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
