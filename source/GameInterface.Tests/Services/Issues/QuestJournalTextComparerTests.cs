using System.Collections.Generic;
using GameInterface.Services.Issues.Framework.Finalization;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class QuestJournalTextComparerTests
{
    [Fact]
    public void SameTextMatchesRecursiveAttributes()
    {
        var first = new TextObject("Deliver {TROOP}", new Dictionary<string, object>
        {
            ["TROOP"] = new TextObject("Imperial trained infantryman")
        });
        var second = new TextObject("Deliver {TROOP}", new Dictionary<string, object>
        {
            ["TROOP"] = new TextObject("Imperial trained infantryman")
        });

        Assert.True(QuestJournalTextComparer.SameText(first, second));
        Assert.False(QuestJournalTextComparer.SameText(
            first,
            new TextObject("Deliver {TROOP}", new Dictionary<string, object>
            {
                ["TROOP"] = new TextObject("Imperial recruit")
            })));
    }

    [Fact]
    public void SameTextAndSameKeyKeepTheirExistingNullAndKeySemantics()
    {
        Assert.True(QuestJournalTextComparer.SameText(null, null));
        Assert.False(QuestJournalTextComparer.SameText(null, new TextObject("text")));
        Assert.True(QuestJournalTextComparer.SameKey(new TextObject("same"), new TextObject("same")));
        Assert.True(QuestJournalTextComparer.SameKey(
            new TextObject("same", new Dictionary<string, object> { ["A"] = 1 }),
            new TextObject("same", new Dictionary<string, object> { ["A"] = 2 })));
        Assert.False(QuestJournalTextComparer.SameKey(new TextObject("one"), new TextObject("two")));
        Assert.True(QuestJournalTextComparer.SameKey(null, null));
        Assert.False(QuestJournalTextComparer.SameKey(null, new TextObject("text")));
    }
}
