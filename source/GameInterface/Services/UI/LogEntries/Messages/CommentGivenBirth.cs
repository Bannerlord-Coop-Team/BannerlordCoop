using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.UI.LogEntries.Messages;

public readonly struct CommentGivenBirth : IEvent
{
    public readonly Hero Mother;
    public readonly List<Hero> AliveChildren;
    public readonly int StillbornCount;

    public CommentGivenBirth(
        Hero mother,
        List<Hero> aliveChildren,
        int stillbornCount)
    {
        Mother = mother;
        AliveChildren = aliveChildren;
        StillbornCount = stillbornCount;
    }
}
