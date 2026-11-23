using Common.Util;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using Missions;
using Missions.Tournaments;
using Moq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Tournaments;

/// <summary>Checks remote tournament startup after replicated spawning.</summary>
public class TournamentRemoteMissionStartTests : MissionTestEnvironment
{
    public TournamentRemoteMissionStartTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReceivedManifest_WithSpawnedMatch_EntersBattleMode(bool localFighter)
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, localFighter ? "fighter" : "watcher");

        client.Call(() =>
        {
            BannerManager.Initialize();
            var mission = fixture.CreateMission(client);
            mission.Shell._missionMode = MissionMode.StartUp;
            var listener = new Mock<IMissionListener>();
            mission.Shell._listeners = new List<IMissionListener> { listener.Object };
            var controller = client.Resolve<CoopTournamentController>();
            Assert.True(controller.Session.TryApplyState(
                "session", "mission", 7, 1, "match", "host", Array.Empty<string>()));
            Assert.False(controller.Session.IsLocalHost);

            var contestant = new TournamentContestantData(
                "slot", "character", 1, "fighter", "Fighter", true, false, true, null);
            var team = new TournamentTeamData("team", new[] { "slot" }, 0, false, 1, null);
            var match = new TournamentMatchData(
                "match", "round", 3, 1, 1, new[] { team }, Array.Empty<string>());
            var snapshot = new TournamentSessionSnapshot(
                "session", "mission", "town", "scene", "prize", TournamentSessionPhase.LiveMatch,
                7, 1, "match", "host", Array.Empty<string>(), new[] { contestant }, Array.Empty<string>(),
                Array.Empty<TournamentPlayerChoiceData>(),
                new[] { new TournamentRoundData("round", 0, 0, new[] { match }) },
                0, 0, 1, false, false, null);

            CharacterObject character = (CharacterObject)Game.Current.PlayerTroop;
            var objects = new Mock<IObjectManager>();
            objects.Setup(manager => manager.TryGetObject("character", out character)).Returns(true);
            var behavior = ObjectHelper.SkipConstructor<CoopTournamentBehavior>();
            new TournamentNativeBracketHydrator(objects.Object).Apply(behavior, snapshot);
            var fight = ObjectHelper.SkipConstructor<CoopTournamentFightMissionController>();

            AgentControllerType control = localFighter ? AgentControllerType.Player : AgentControllerType.None;
            Agent agent = mission.SpawnAgent(new AgentBuildData(character).Controller(control));
            if (localFighter) mission.MainAgent = agent;
            Guid agentId = Guid.NewGuid();
            var registry = client.Resolve<INetworkAgentRegistry>();
            Assert.True(registry.TryRegisterAgent("fighter", agentId, agent));
            var spawn = new TournamentAgentSpawnData(
                agentId, "slot", "character", 1, "team", 1, null, "fighter",
                Array.Empty<EquipmentElement>(), Vec3.Zero, new Vec2(0, 1), 100,
                Guid.Empty, null, 0, Array.Empty<EquipmentElement>(), 0);
            var manifest = new TournamentSpawnManifestData("session", "match", 7, 1, 1, new[] { spawn });
            Assert.True(TournamentSpawnManifestValidator.IsValid(manifest, snapshot));

            SetField(controller, "snapshot", snapshot);
            SetField(controller, "tournamentBehavior", behavior);
            SetField(controller, "fightController", fight);
            SetField(controller, "pendingApplyManifest", manifest);
            SetField(controller, "missionReadyForManifest", true);
            MethodInfo apply = typeof(CoopTournamentController).GetMethod(
                "ApplyPendingManifest", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(apply);
            apply.Invoke(controller, null);

            Assert.Same(behavior.CurrentMatch, fight._match);
            Assert.Same(localFighter ? agent : null, mission.MainAgent);
            Assert.True(agent.IsActive());
            Assert.Equal(control, agent.Controller);
            Assert.Equal(MissionMode.Battle, mission.Shell.Mode);

            SetField(controller, "pendingApplyManifest", manifest);
            apply.Invoke(controller, null);

            Assert.Equal(MissionMode.Battle, mission.Shell.Mode);
            Assert.Same(agent, Assert.Single(mission.Agents));
            listener.Verify(value => value.OnMissionModeChange(MissionMode.StartUp, true), Times.Once);
            listener.VerifyNoOtherCalls();
        });
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(target, value);
    }
}
