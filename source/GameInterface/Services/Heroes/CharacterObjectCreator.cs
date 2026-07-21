using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes;

/// <summary>
/// Creates runtime character objects from campaign templates.
/// </summary>
internal interface ICharacterObjectCreator
{
    CharacterObject CreateFrom(CharacterObject template);
}

/// <inheritdoc cref="ICharacterObjectCreator"/>
internal class CharacterObjectCreator : ICharacterObjectCreator
{
    public CharacterObject CreateFrom(CharacterObject template)
    {
        return CharacterObject.CreateFrom(template);
    }
}
