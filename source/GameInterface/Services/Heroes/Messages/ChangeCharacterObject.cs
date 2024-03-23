using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;
/// <summary>
/// Client publish for _characterObject
/// </summary>
public record ChangeCharacterObject : ICommand
{
    public string CharacterObjectId { get; }
    public string HeroId { get; }

    public ChangeCharacterObject(string characterObjectId, string heroId)
    {
        CharacterObjectId = characterObjectId;
        HeroId = heroId;
    }
}