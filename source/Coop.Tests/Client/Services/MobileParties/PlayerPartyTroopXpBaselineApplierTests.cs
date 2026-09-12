using Common.Util;
using Coop.Core.Client.Services.MobileParties;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace Coop.Tests.Client.Services.MobileParties;

public class PlayerPartyTroopXpBaselineApplierTests
{
    [Fact]
    public void TryApply_RestoresMemberAndPrisonerXp()
    {
        var memberRoster = new TroopRoster();
        var prisonerRoster = new TroopRoster();
        var member = new CharacterObject();
        var prisoner = new CharacterObject();
        memberRoster.AddToCounts(member, 3);
        prisonerRoster.AddToCounts(prisoner, 2);

        var objectManager = new Mock<IObjectManager>();
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging("members", out memberRoster))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging("prisoners", out prisonerRoster))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging("member", out member))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging("prisoner", out prisoner))
            .Returns(true);
        var applier = new PlayerPartyTroopXpBaselineApplier(objectManager.Object);

        bool applied = applier.TryApply(new[]
        {
            new TroopRosterXpBaseline("members", new[]
            {
                new TroopXpBaselineEntry("member", 123),
            }),
            new TroopRosterXpBaseline("prisoners", new[]
            {
                new TroopXpBaselineEntry("prisoner", 456),
            }),
        });

        Assert.True(applied);
        Assert.Equal(123, memberRoster.GetElementCopyAtIndex(0).Xp);
        Assert.Equal(456, prisonerRoster.GetElementCopyAtIndex(0).Xp);
        Assert.False(AllowedThread.IsThisThreadAllowed());
    }

    [Fact]
    public void TryApply_MissingEntryDoesNotPartiallyMutateResolvedEntries()
    {
        var roster = new TroopRoster();
        var character = new CharacterObject();
        roster.AddToCounts(character, 1, xpChange: 10);

        CharacterObject missing = null;
        var objectManager = new Mock<IObjectManager>();
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging("roster", out roster))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging("character", out character))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging("missing", out missing))
            .Returns(false);
        var applier = new PlayerPartyTroopXpBaselineApplier(objectManager.Object);

        bool applied = applier.TryApply(new[]
        {
            new TroopRosterXpBaseline("roster", new[]
            {
                new TroopXpBaselineEntry("character", 100),
                new TroopXpBaselineEntry("missing", 200),
            }),
        });

        Assert.False(applied);
        Assert.Equal(10, roster.GetElementCopyAtIndex(0).Xp);
        Assert.False(AllowedThread.IsThisThreadAllowed());
    }
}
