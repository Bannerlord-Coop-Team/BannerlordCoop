using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event from GameInterface for TattooTags.
/// </summary>
public readonly struct TattooTagsChanged : IEvent
{
    public readonly string TattooTags;
    public readonly Hero Hero;

    public TattooTagsChanged(string tattooTags, Hero hero)
    {
        TattooTags = tattooTags;
        Hero = hero;
    }
}