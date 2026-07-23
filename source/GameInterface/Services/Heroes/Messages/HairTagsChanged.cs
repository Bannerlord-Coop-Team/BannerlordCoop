using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event from GameInterface for HairTags.
/// </summary>
public readonly struct HairTagsChanged : IEvent
{
    public readonly string HairTags;
    public readonly Hero Hero;

    public HairTagsChanged(string hairTags, Hero hero)
    {
        HairTags = hairTags;
        Hero = hero;
    }
}