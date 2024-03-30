using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;
/// <summary>
/// Client publish for HairTags
/// </summary>
public record ChangeHairTags : ICommand
{
    public string HairTags { get; }
    public string HeroId { get; }

    public ChangeHairTags(string hairTags, string heroId)
    {
        HairTags = hairTags;
        HeroId = heroId;
    }
}