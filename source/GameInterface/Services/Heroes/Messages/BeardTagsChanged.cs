using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event from GameInterface for BeardTags.
/// </summary>
public readonly struct BeardTagsChanged : IEvent
{
    public readonly string BeardTags;
    public readonly Hero Hero;

    public BeardTagsChanged(string beardTags, Hero hero)
    {
        BeardTags = beardTags;
        Hero = hero;
    }
}