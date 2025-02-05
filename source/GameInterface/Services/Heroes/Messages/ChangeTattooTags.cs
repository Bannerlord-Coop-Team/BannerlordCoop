using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;
/// <summary>
/// Client publish for TattooTags
/// </summary>
public record ChangeTattooTags : ICommand
{
    public string TattooTags { get; }
    public string HeroId { get; }

    public ChangeTattooTags(string tattooTags, string heroId)
    {
        TattooTags = tattooTags;
        HeroId = heroId;
    }
}