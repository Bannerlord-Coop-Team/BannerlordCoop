using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Heroes;

/// <summary>
/// Creates and initializes runtime character objects.
/// </summary>
internal interface ICharacterObjectCreator
{
    CharacterObject CreateUnregistered(string stringId);
    void RegisterAndInitializeFrom(CharacterObject characterObject, CharacterObject template);
}

/// <inheritdoc cref="ICharacterObjectCreator"/>
internal class CharacterObjectCreator : ICharacterObjectCreator
{
    public CharacterObject CreateUnregistered(string stringId)
    {
        return new CharacterObject
        {
            StringId = stringId,
        };
    }

    public void RegisterAndInitializeFrom(CharacterObject characterObject, CharacterObject template)
    {
        MBObjectManager.Instance.RegisterObject(characterObject);
        characterObject._originCharacter = template;
        characterObject.InitializeHeroCharacterOnAfterLoad();
    }
}
