using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.UI.LogEntries.Messages;

public readonly struct CommentHeroKilled : IEvent
{
    public readonly Hero Victim;
    public readonly Hero Killer;
    public readonly KillCharacterAction.KillCharacterActionDetail Detail;

    public CommentHeroKilled(
        Hero victim,
        Hero killer,
        KillCharacterAction.KillCharacterActionDetail detail)
    {
        Victim = victim;
        Killer = killer;
        Detail = detail;
    }
}
