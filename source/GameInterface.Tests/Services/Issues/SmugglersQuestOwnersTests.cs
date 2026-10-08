using Common.Util;
using GameInterface.Services.Issues;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class SmugglersQuestOwnersTests
{
    [Fact]
    public void HeirInheritsOnlyPreviousPlayersJournalOwnership()
    {
        var oldPlayer = ObjectHelper.SkipConstructor<Hero>();
        var heir = ObjectHelper.SkipConstructor<Hero>();
        var otherPlayer = ObjectHelper.SkipConstructor<Hero>();
        var quest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
        var alternative = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssue>();
        var otherQuest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
        var owners = new SmugglersQuestOwners();
        owners.Set(quest, oldPlayer);
        owners.Set(alternative, oldPlayer);
        owners.Set(otherQuest, otherPlayer);

        owners.ReplacePlayer(oldPlayer, heir);
        var restored = new SmugglersQuestOwners();
        restored.Restore(owners.Snapshot());

        Assert.True(restored.TryGet(quest, out var questOwner));
        Assert.Same(heir, questOwner);
        Assert.True(restored.TryGet(alternative, out var alternativeOwner));
        Assert.Same(heir, alternativeOwner);
        Assert.True(restored.TryGet(otherQuest, out var otherOwner));
        Assert.Same(otherPlayer, otherOwner);
    }

    [Fact]
    public void RestoredQuestAndAlternativeRetainDifferentPlayers()
    {
        var quest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
        var alternative = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssue>();
        var firstPlayer = ObjectHelper.SkipConstructor<Hero>();
        var secondPlayer = ObjectHelper.SkipConstructor<Hero>();
        var owners = new SmugglersQuestOwners();
        owners.Set(quest, firstPlayer);
        owners.Set(alternative, secondPlayer);

        var restored = new SmugglersQuestOwners();
        restored.Restore(owners.Snapshot());

        Assert.True(restored.TryGet(quest, out var questOwner));
        Assert.Same(firstPlayer, questOwner);
        Assert.True(restored.TryGet(alternative, out var alternativeOwner));
        Assert.Same(secondPlayer, alternativeOwner);
    }

    [Fact]
    public void LaterQuestDoesNotReplaceArchivedQuestOwnership()
    {
        var oldQuest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
        var newQuest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
        var firstPlayer = ObjectHelper.SkipConstructor<Hero>();
        var secondPlayer = ObjectHelper.SkipConstructor<Hero>();
        var owners = new SmugglersQuestOwners();
        owners.Set(oldQuest, firstPlayer);
        owners.Set(newQuest, secondPlayer);

        Assert.True(owners.TryGet(oldQuest, out var owner));
        Assert.Same(firstPlayer, owner);
        Assert.Equal(2, owners.Snapshot().Count);
    }

    [Fact]
    public void RestoreDoesNotKeepPreviousCampaignOrUnrelatedObjects()
    {
        var quest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var owners = new SmugglersQuestOwners();
        owners.Set(quest, hero);

        owners.Restore(new Dictionary<MBObjectBase, Hero> { [hero] = hero });

        Assert.Empty(owners.Snapshot());
        Assert.False(owners.TryGet(quest, out _));
    }

    [Fact]
    public void SnapshotCannotChangeSessionOwnership()
    {
        var quest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var owners = new SmugglersQuestOwners();
        owners.Set(quest, hero);

        owners.Snapshot().Clear();

        Assert.True(owners.TryGet(quest, out var owner));
        Assert.Same(hero, owner);
    }
}
