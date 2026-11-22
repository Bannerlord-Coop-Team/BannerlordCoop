using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.UI.LogEntries.Messages;

public readonly struct CommentClanLeaderChanged : IEvent
{
    public readonly Hero OldLeader;
    public readonly Hero NewLeader;

    public CommentClanLeaderChanged(Hero oldLeader, Hero newLeader)
    {
        OldLeader = oldLeader;
        NewLeader = newLeader;
    }
}
