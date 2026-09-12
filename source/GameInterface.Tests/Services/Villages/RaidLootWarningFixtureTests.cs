using Common.Commands;
using GameInterface.Services.Villages.Commands;
using System;
using System.Linq;
using Xunit;
#if DEBUG
using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
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
#else
        Assert.DoesNotContain("PrepareRaidLootWarningCoopCommand", names);
        Assert.DoesNotContain("RaidLootWarningStateCoopCommand", names);
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
                new Mock<IMessageBroker>(MockBehavior.Strict).Object);
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
                new Mock<IMessageBroker>(MockBehavior.Strict).Object);
            Assert.False(fixture.Prepare("only-connected", "disposable-baseline").Succeeded);
        }
        finally
        {
            ModInformation.IsServer = previous;
        }
    }

    [Fact]
    public void FixtureCommands_DeclareAuthorityAndReadOnlyObservationSides()
    {
        var fixture = new Mock<IRaidLootWarningFixture>().Object;
        Assert.Equal(CoopCommandSide.Server, new RaidDebugCommands.PrepareRaidLootWarningCoopCommand(fixture).Side);
        Assert.Equal(CoopCommandSide.Both, new RaidDebugCommands.RaidLootWarningStateCoopCommand(fixture).Side);
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
#endif
}
