using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.UI.LogEntries.Messages;

public readonly struct CommentCharacterBorn : IEvent
{
    public readonly Hero Hero;

    public CommentCharacterBorn(Hero hero)
    {
        Hero = hero;
    }
}
