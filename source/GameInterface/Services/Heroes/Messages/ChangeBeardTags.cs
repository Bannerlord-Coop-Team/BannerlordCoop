using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;
/// <summary>
/// Client publish for BeardTags
/// </summary>
public record ChangeBeardTags : ICommand
{
    public string BeardTags { get; }
    public string HeroId { get; }

    public ChangeBeardTags(string beardTags, string heroId)
    {
        BeardTags = beardTags;
        HeroId = heroId;
    }
}