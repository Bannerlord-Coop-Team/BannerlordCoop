using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Heroes;

/// <summary>
/// Creates and initializes runtime character objects.
/// </summary>
internal interface ICharacterObjectCreator
{
    CharacterObject Create(string stringId);
    CharacterObject CreateFrom(CharacterObject template);
    void InitializeFrom(CharacterObject characterObject, CharacterObject template);
}

/// <inheritdoc cref="ICharacterObjectCreator"/>
internal class CharacterObjectCreator : ICharacterObjectCreator
{
    public CharacterObject Create(string stringId)
    {
        return MBObjectManager.Instance.CreateObject<CharacterObject>(stringId);
    }

    public CharacterObject CreateFrom(CharacterObject template)
    {
        return CharacterObject.CreateFrom(template);
    }

    public void InitializeFrom(CharacterObject characterObject, CharacterObject template)
    {
        characterObject._originCharacter = template;
        characterObject.InitializeHeroCharacterOnAfterLoad();
    }
}
