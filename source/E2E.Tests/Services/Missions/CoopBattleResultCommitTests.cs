using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using System;
using System.Linq;
using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents.Messages;
using Missions.Battles;
using Moq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// The fix for the live "lost coop battle leaves the players uncaptured with the encounter still open" bug. A
/// live coop battle never resolves the campaign result on its own (the encounter doesn't tick), so on the host's
/// mission end <see cref="CoopBattleController"/> commits the concluded <see cref="MissionResult"/>'s BattleState
/// to the map event. That commit flows through the coop intercept to the server, which runs the (separately
/// tested) capture + auto-finalize. This drives the host-side trigger and asserts the result is committed.
/// </summary>
public class CoopBattleResultCommitTests : MissionTestEnvironment
{
    public CoopBattleResultCommitTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void HostConcludesLostBattle_CommitsDefeatResult_ToCampaign()
    {
        using var fixture = new MissionEngineFixture();
        var host = Clients.First();
        SetControllerId(host, "host");

        BattleState committed = BattleState.None;

        host.Call(() =>
        {
            // The host's mission concluded as a player DEFEAT — the AI (defender) won.
            var mock = fixture.CreateMission(host);
            mock.Shell.MissionResult = new MissionResult(BattleState.DefenderVictory, playerVictory: false, playerDefeated: true, enemyRetreated: false);

            var controller = host.Resolve<CoopBattleController>();
            controller.Session.TryBegin("mapEvent1");
            host.Resolve<IBattleHostRegistry>().Set("mapEvent1", new BattleHostAssignment("host", Array.Empty<string>()));

            // The intercept publishes before the native BattleState setter runs; give the (constructor-skipped)
            // map event an empty sides array so the client-skipped OnBattleWon prefix doesn't deref a null _sides.
            var mapEvent = host.CreateRegisteredObject<MapEvent>("mapEvent1");
            mapEvent._sides = Array.Empty<MapEventSide>();

            // Capture what the coop intercept forwards to the campaign sync.
            host.Resolve<IMessageBroker>().Subscribe<MapEventBattleStateChangeAttempted>(p => committed = p.What.BattleState);

            // The coop battle concluded on the host -> it commits the result to the campaign map event.
            controller.ResultCommitter.CommitResolvedResult();

            GC.KeepAlive(controller);
        });

        // The host committed the AI's victory, which the server applies (OnBattleWon -> capture) and auto-finalizes.
        Assert.Equal(BattleState.DefenderVictory, committed);
    }

    [Fact]
    public void SuccessorLeavesWithResolvedVictory_SubmitsValidatedFallback()
    {
        using var fixture = new MissionEngineFixture();
        var successor = Clients.First();

        successor.Call(() =>
        {
            var mock = fixture.CreateMission(successor);
            mock.Shell.MissionResult = new MissionResult(BattleState.AttackerVictory,
                playerVictory: true, playerDefeated: false, enemyRetreated: false);

            var mapEvent = successor.CreateRegisteredObject<MapEvent>("mapEvent1");
            mapEvent._battleState = BattleState.AttackerVictory;

            var objectManager = new Mock<IObjectManager>();
            var session = new Mock<IBattleSession>();
            var hostRegistry = new Mock<IBattleHostRegistry>();
            var network = new Mock<INetwork>();
            IMessage sent = null;

            objectManager.Setup(x => x.TryGetObject<MapEvent>("mapEvent1", out mapEvent)).Returns(true);
            session.SetupGet(x => x.InstanceId).Returns("mapEvent1");
            session.SetupGet(x => x.IsLocalHost).Returns(false);
            network.Setup(x => x.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(message => sent = message);

            var committer = new BattleResultCommitter(objectManager.Object, session.Object, hostRegistry.Object, network.Object);
            committer.CommitResolvedResult();

            var fallback = Assert.IsType<NetworkChangeBattleState>(sent);
            Assert.Equal("mapEvent1", fallback.MapEventId);
            Assert.Equal(BattleState.AttackerVictory, fallback.BattleState);
            Assert.True(fallback.IsLeavingFallback);
        });
    }

    /// <summary>
    /// A host leaving a battle that OTHER players are still fighting (successor line non-empty) must NOT commit
    /// its mission result: a host retreating after losing its own troops carries a RESOLVED defeat result, and
    /// committing it made the server conclude the live battle under the successor — capturing the still-fighting
    /// players and destroying the map event beneath their mission. The last player out commits instead.
    /// </summary>
    [Fact]
    public void HostLeavesWithSuccessorsStillFighting_DoesNotCommitTheResult()
    {
        using var fixture = new MissionEngineFixture();
        var host = Clients.First();
        SetControllerId(host, "host");

        BattleState committed = BattleState.None;

        host.Call(() =>
        {
            // The retreating host's mission "concluded" as a player DEFEAT (its own troops were wiped before it
            // fled) — a RESOLVED result that must still not be committed while a successor fights on.
            var mock = fixture.CreateMission(host);
            mock.Shell.MissionResult = new MissionResult(BattleState.DefenderVictory, playerVictory: false, playerDefeated: true, enemyRetreated: false);

            var controller = host.Resolve<CoopBattleController>();
            controller.Session.TryBegin("mapEvent1");
            host.Resolve<IBattleHostRegistry>().Set("mapEvent1", new BattleHostAssignment("host", new[] { "successor" }));

            var mapEvent = host.CreateRegisteredObject<MapEvent>("mapEvent1");
            mapEvent._sides = Array.Empty<MapEventSide>();

            host.Resolve<IMessageBroker>().Subscribe<MapEventBattleStateChangeAttempted>(p => committed = p.What.BattleState);

            controller.ResultCommitter.CommitResolvedResult();

            GC.KeepAlive(controller);
        });

        Assert.Equal(BattleState.None, committed);
    }
}
