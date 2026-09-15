using Common.Commands;
using GameInterface.Services.Villages.Commands;
using System;
using System.Linq;
using Xunit;
#if DEBUG
using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Logging;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using GameInterface.Services.MapEvents.Messages.Start;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Villages.Interfaces;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
#endif

namespace GameInterface.Tests.Services.Villages;

public class RaidLootWarningFixtureTests
{
    [Fact]
    public void FixtureCommands_AreOnlyIncludedInDebugBuilds()
    {
        var names = typeof(RaidDebugCommands).GetNestedTypes().Select(type => type.Name).ToArray();
#if DEBUG
        Assert.Contains("PrepareRaidLootWarningCoopCommand", names);
        Assert.Contains("RaidLootWarningStateCoopCommand", names);
        Assert.Contains("StartRaidLootWarningCoopCommand", names);
        Assert.Contains("RequestRaidLootWarningCoopCommand", names);
        Assert.Contains("CompleteRaidLootWarningSimulationCoopCommand", names);
        Assert.Contains("CompleteRaidLootWarningPartyCoopCommand", names);
        Assert.Contains("ShowRaidLootWarningCoopCommand", names);
        Assert.Contains("AcceptRaidLootWarningCoopCommand", names);
#else
        Assert.DoesNotContain("PrepareRaidLootWarningCoopCommand", names);
        Assert.DoesNotContain("RaidLootWarningStateCoopCommand", names);
        Assert.DoesNotContain("StartRaidLootWarningCoopCommand", names);
        Assert.DoesNotContain("RequestRaidLootWarningCoopCommand", names);
        Assert.DoesNotContain("CompleteRaidLootWarningSimulationCoopCommand", names);
        Assert.DoesNotContain("CompleteRaidLootWarningPartyCoopCommand", names);
        Assert.DoesNotContain("ShowRaidLootWarningCoopCommand", names);
        Assert.DoesNotContain("AcceptRaidLootWarningCoopCommand", names);
        Assert.Null(typeof(RaidDebugCommands).Assembly.GetType("GameInterface.Services.Villages.Commands.RaidLootWarningFixture"));
#endif
        Assert.Contains("AllowRaidAiInterventionCoopCommand", names);
    }

#if DEBUG
    [Fact]
    public void Prepare_WithoutDisposableBaseline_DoesNotResolveOrMutateGameState()
    {
        bool previous = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            var fixture = new RaidLootWarningFixture(
                new Mock<IObjectManager>(MockBehavior.Strict).Object,
                new Mock<IPlayerManager>(MockBehavior.Strict).Object,
                new Mock<IMessageBroker>(MockBehavior.Strict).Object,
                new Mock<IVillageHostileActionInterface>(MockBehavior.Strict).Object);
            var result = fixture.Prepare("only-connected", "reusable-save");
            Assert.False(result.Succeeded);
            Assert.Contains("disposable-baseline", result.Output);
        }
        finally
        {
            ModInformation.IsServer = previous;
        }
    }

    [Fact]
    public void Prepare_OnClient_IsRejectedBeforeResolvingGameState()
    {
        bool previous = ModInformation.IsServer;
        ModInformation.IsServer = false;
        try
        {
            var fixture = new RaidLootWarningFixture(
                new Mock<IObjectManager>(MockBehavior.Strict).Object,
                new Mock<IPlayerManager>(MockBehavior.Strict).Object,
                new Mock<IMessageBroker>(MockBehavior.Strict).Object,
                new Mock<IVillageHostileActionInterface>(MockBehavior.Strict).Object);
            Assert.False(fixture.Prepare("only-connected", "disposable-baseline").Succeeded);
        }
        finally
        {
            ModInformation.IsServer = previous;
        }
    }

    [Fact]
    public void FixtureCommands_DeclareAuthorityAndClientActionSides()
    {
        var fixture = new Mock<IRaidLootWarningFixture>().Object;
        Assert.Equal(CoopCommandSide.Server, new RaidDebugCommands.PrepareRaidLootWarningCoopCommand(fixture).Side);
        Assert.Equal(CoopCommandSide.Both, new RaidDebugCommands.RaidLootWarningStateCoopCommand(fixture).Side);
        Assert.Equal(CoopCommandSide.Client, new RaidDebugCommands.StartRaidLootWarningCoopCommand(fixture).Side);
        Assert.Equal(CoopCommandSide.Client, new RaidDebugCommands.RequestRaidLootWarningCoopCommand(fixture).Side);
        Assert.Equal(CoopCommandSide.Both, new RaidDebugCommands.CompleteRaidLootWarningSimulationCoopCommand(fixture).Side);
        Assert.Equal(CoopCommandSide.Client, new RaidDebugCommands.CompleteRaidLootWarningPartyCoopCommand(fixture).Side);
        Assert.Equal(CoopCommandSide.Client, new RaidDebugCommands.ShowRaidLootWarningCoopCommand(fixture).Side);
        Assert.Equal(CoopCommandSide.Client, new RaidDebugCommands.AcceptRaidLootWarningCoopCommand(fixture).Side);
    }

    [Fact]
    public void ClientActionSession_RequiresObservedSettlementEntryBeforeRequestingRaid()
    {
        var session = CreateClientActionSession();

        Assert.Throws<InvalidOperationException>(() => session.RequestRaid());
        session.RequestSettlementEntry();
        Assert.Equal("settlement-entry-requested", session.Phase);
        Assert.True(session.IsAwaitingSettlementEntryApproval);
        Assert.False(session.CanRequestRaid());
        Assert.Throws<InvalidOperationException>(() => session.RequestSettlementEntry());
        Assert.Throws<InvalidOperationException>(() => session.RequestRaid());

        session.ObserveSettlementEntryApproval();
        Assert.Equal("settlement-entry-approved", session.Phase);
        Assert.True(session.CanRequestRaid());
        session.RequestRaid();
        Assert.Equal("raid-requested", session.Phase);
        Assert.Throws<InvalidOperationException>(() => session.RequestRaid());
    }

    [Fact]
    public void PreparedSession_OnlyAllowsSameCampaignAndTargetToRepeat()
    {
        var session = CreateSession();
        session.Prepare(() => { });
        Assert.True(session.CanRepeat(session.Campaign, session.ControllerId, session.Party, session.Settlement));
        Assert.False(session.CanRepeat(ObjectHelper.SkipConstructor<Campaign>(), session.ControllerId, session.Party, session.Settlement));
        Assert.False(session.CanRepeat(session.Campaign, "another-controller", session.Party, session.Settlement));
        Assert.False(session.CanRepeat(session.Campaign, session.ControllerId, ObjectHelper.SkipConstructor<MobileParty>(), session.Settlement));
        Assert.False(session.CanRepeat(session.Campaign, session.ControllerId, session.Party, ObjectHelper.SkipConstructor<Settlement>()));
    }

    [Fact]
    public void PartialPreparationFailure_CannotBeRetriedOrCaptured()
    {
        var session = CreateSession();
        int mutations = 0;
        Assert.Throws<InvalidOperationException>(() => session.Prepare(() =>
        {
            mutations++;
            throw new InvalidOperationException("partial mutation");
        }));
        Assert.Throws<InvalidOperationException>(() => session.Prepare(() => mutations++));
        session.Capture(session.Campaign, session.Party, session.Settlement, ObjectHelper.SkipConstructor<MapEvent>(), "event-a");
        Assert.Equal(1, mutations);
        Assert.Equal("failed-reload-baseline", session.Phase);
        Assert.Null(session.MapEvent);
    }

    [Fact]
    public void Capture_RejectsWrongCampaignPartyAndVillage_AndBindsOnlyFirstEvent()
    {
        var session = CreateSession();
        session.Prepare(() => { });
        var first = ObjectHelper.SkipConstructor<MapEvent>();
        var later = ObjectHelper.SkipConstructor<MapEvent>();
        session.Capture(ObjectHelper.SkipConstructor<Campaign>(), session.Party, session.Settlement, first, "wrong-campaign");
        session.Capture(session.Campaign, ObjectHelper.SkipConstructor<MobileParty>(), session.Settlement, first, "wrong-party");
        session.Capture(session.Campaign, session.Party, ObjectHelper.SkipConstructor<Settlement>(), first, "wrong-village");
        Assert.Null(session.MapEvent);
        session.Capture(session.Campaign, session.Party, session.Settlement, first, "event-a");
        session.Capture(session.Campaign, session.Party, session.Settlement, later, "event-b");
        Assert.Same(first, session.MapEvent);
        Assert.Equal("event-a", session.MapEventId);
        Assert.False(session.CanRepeat(session.Campaign, session.ControllerId, session.Party, session.Settlement));
    }

    [Fact]
    public void SimulationAdvance_RequiresCaptureAndPublishesTheBoundProductionRequestOnce()
    {
        var session = CreateSession();
        var broker = new Mock<IMessageBroker>(MockBehavior.Strict);
        Assert.Throws<InvalidOperationException>(() => session.RequestSimulationAdvance(broker.Object));
        session.Prepare(() => { });
        Assert.Throws<InvalidOperationException>(() => session.RequestSimulationAdvance(broker.Object));
        session.Capture(session.Campaign, session.Party, session.Settlement, ObjectHelper.SkipConstructor<MapEvent>(), "event-a");
        broker.Setup(x => x.Publish(session, It.Is<NetworkAdvanceBattleSimulation>(m => m.MapEventId == "event-a" && m.MaxRounds == int.MaxValue)));
        session.RequestSimulationAdvance(broker.Object);
        Assert.True(session.SimulationAdvanceRequested);
        Assert.Equal("captured", session.Phase);
        Assert.Throws<InvalidOperationException>(() => session.RequestSimulationAdvance(broker.Object));
        broker.Verify(x => x.Publish(session, It.IsAny<NetworkAdvanceBattleSimulation>()), Times.Once);
    }

    [Fact]
    public void SimulationAdvance_PartialDispatchFailureCannotBeRetried()
    {
        var session = CreateSession();
        session.Prepare(() => { });
        session.Capture(session.Campaign, session.Party, session.Settlement, ObjectHelper.SkipConstructor<MapEvent>(), "event-a");
        var broker = new Mock<IMessageBroker>();
        broker.Setup(x => x.Publish(session, It.IsAny<NetworkAdvanceBattleSimulation>())).Throws<InvalidOperationException>();
        Assert.Throws<InvalidOperationException>(() => session.RequestSimulationAdvance(broker.Object));
        Assert.Throws<InvalidOperationException>(() => session.RequestSimulationAdvance(broker.Object));
        broker.Verify(x => x.Publish(session, It.IsAny<NetworkAdvanceBattleSimulation>()), Times.Once);
    }

    [Theory]
    [InlineData(false, true, "dispatching")]
    [InlineData(true, false, "server-role-check")]
    [InlineData(true, true, "session-lookup")]
    public void SimulationDiagnostics_DistinguishNoSubscriberWrongRoleAndMissingSession(bool subscribed, bool server, string stage)
    {
        bool previous = ModInformation.IsServer;
        ModInformation.IsServer = server;
        try
        {
            using var broker = new MessageBroker();
            using var handler = subscribed ? new BattleSimulationRunHandler(broker, new Mock<INetwork>().Object,
                new Mock<IObjectManager>().Object, new Mock<IMapEventLogger>().Object, new Mock<IPlayerManager>().Object) : null;
            var session = CreateSession();
            session.Prepare(() => { });
            session.Capture(session.Campaign, session.Party, session.Settlement, ObjectHelper.SkipConstructor<MapEvent>(), "event-a");
            session.RequestSimulationAdvance(broker);
            Assert.Equal(stage, session.SimulationAdvanceStage);
            Assert.Null(session.SimulationSessionMatches);
            Assert.Null(session.SimulationAdvanceException);
            Assert.Equal(0, session.SimulationRoundsEntered);
            Assert.False(session.SimulationAdvanceCompleted);
            Assert.Throws<InvalidOperationException>(() => session.RequestSimulationAdvance(broker));
        }
        finally
        {
            ModInformation.IsServer = previous;
        }
    }

    [Fact]
    public void SimulationDiagnostics_PreserveSessionMismatchAndCaughtGameThreadException()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
        bool previous = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            using var broker = new MessageBroker();
            using var handler = new BattleSimulationRunHandler(broker, new Mock<INetwork>().Object,
                new Mock<IObjectManager>().Object, new Mock<IMapEventLogger>().Object, new Mock<IPlayerManager>().Object);
            var session = CreateSession();
            session.Prepare(() => { });
            session.Capture(session.Campaign, session.Party, session.Settlement, ObjectHelper.SkipConstructor<MapEvent>(), "event-a");
            // A corrupt private handler session must report its identity mismatch and the swallowed exception.
            var activeType = typeof(BattleSimulationRunHandler).GetNestedType("ActiveSimulation", BindingFlags.NonPublic);
            var sessions = (IDictionary)typeof(BattleSimulationRunHandler).GetField("activeSimulations", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(handler);
            sessions.Add("event-a", Activator.CreateInstance(activeType, true));
            GameThread.Run(() => session.RequestSimulationAdvance(broker), blocking: true);
            Assert.False(session.SimulationSessionMatches);
            Assert.Contains(nameof(NullReferenceException), session.SimulationAdvanceException);
            Assert.Contains("Handle_NetworkAdvanceBattleSimulation", session.SimulationAdvanceException);
            Assert.Equal(0, session.SimulationRoundsEntered);
            Assert.Equal(0, session.SimulationRoundsCompleted);
            Assert.False(session.SimulationAdvanceCompleted);
            Assert.Throws<InvalidOperationException>(() => session.RequestSimulationAdvance(broker));
        }
        finally
        {
            ModInformation.IsServer = previous;
        }
    }

    [Fact]
    public void Seed_RequiresCapturedEventCampaignAndWinningParty_AndRunsOnce()
    {
        var session = CreateSession();
        var captured = ObjectHelper.SkipConstructor<MapEvent>();
        int seeds = 0;
        session.Seed(session.Campaign, captured, session.Party, () => seeds++);
        session.Prepare(() => { });
        session.Capture(session.Campaign, session.Party, session.Settlement, captured, "event-a");
        session.Seed(ObjectHelper.SkipConstructor<Campaign>(), captured, session.Party, () => seeds++);
        session.Seed(session.Campaign, ObjectHelper.SkipConstructor<MapEvent>(), session.Party, () => seeds++);
        session.Seed(session.Campaign, captured, ObjectHelper.SkipConstructor<MobileParty>(), () => seeds++);
        Assert.Equal(0, seeds);
        session.Seed(session.Campaign, captured, session.Party, () => seeds++);
        session.Seed(session.Campaign, captured, session.Party, () => seeds++);
        Assert.Equal(1, seeds);
        Assert.True(session.Seeded);
        Assert.Equal("seeded", session.Phase);
    }

    [Fact]
    public void PartialSeedFailure_RequiresBaselineReload()
    {
        var session = CreateSession();
        var captured = ObjectHelper.SkipConstructor<MapEvent>();
        session.Prepare(() => { });
        session.Capture(session.Campaign, session.Party, session.Settlement, captured, "event-a");
        int seeds = 0;
        Assert.Throws<InvalidOperationException>(() => session.Seed(session.Campaign, captured, session.Party, () =>
        {
            seeds++;
            throw new InvalidOperationException("partial seed");
        }));
        session.Seed(session.Campaign, captured, session.Party, () => seeds++);
        Assert.Equal(1, seeds);
        Assert.False(session.Seeded);
        Assert.Equal("failed-reload-baseline", session.Phase);
    }

    [Fact]
    public void OccupantGuard_AllowsOnlyTargetPlayerAndNativeMilitia()
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var player = ObjectHelper.SkipConstructor<MobileParty>();
        var militia = ObjectHelper.SkipConstructor<MobileParty>();
        var visitor = ObjectHelper.SkipConstructor<MobileParty>();
        visitor._currentSettlement = settlement;
        Assert.False(RaidLootWarningFixture.IsUnexpectedOccupant(player, player, militia, settlement));
        Assert.False(RaidLootWarningFixture.IsUnexpectedOccupant(militia, player, militia, settlement));
        Assert.True(RaidLootWarningFixture.IsUnexpectedOccupant(visitor, player, militia, settlement));
        visitor._currentSettlement = null;
        Assert.False(RaidLootWarningFixture.IsUnexpectedOccupant(visitor, player, militia, settlement));
    }

    [Fact]
    public void RejectedParticipantSet_CannotSeedOrRepeatPreparation()
    {
        var session = CreateSession();
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        session.Prepare(() => { });
        session.Capture(session.Campaign, session.Party, session.Settlement, mapEvent, "raid");
        session.Reject();
        int seeds = 0;
        session.Seed(session.Campaign, mapEvent, session.Party, () => seeds++);
        Assert.Equal(0, seeds);
        Assert.False(session.CanRepeat(session.Campaign, session.ControllerId, session.Party, session.Settlement));
    }

    private static RaidLootWarningFixture.FixtureSession CreateSession() => new(
        ObjectHelper.SkipConstructor<Campaign>(), "fixture-controller",
        ObjectHelper.SkipConstructor<MobileParty>(), ObjectHelper.SkipConstructor<Settlement>());

    private static RaidLootWarningFixture.ClientActionSession CreateClientActionSession() => new(
        ObjectHelper.SkipConstructor<Campaign>(), "fixture-controller",
        ObjectHelper.SkipConstructor<MobileParty>(), ObjectHelper.SkipConstructor<Settlement>());
#endif
}
