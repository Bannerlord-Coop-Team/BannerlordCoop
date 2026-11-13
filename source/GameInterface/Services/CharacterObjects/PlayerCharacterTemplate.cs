using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CharacterObjects;

/// <summary>
/// Gives a joined player's character the player template as its template link (#4675).
/// </summary>
internal static class PlayerCharacterTemplate
{
    /// <summary>The ModuleData character that is every new player's character, as the engine names it in <c>Campaign.InitializeGamePlayReferences</c>.</summary>
    public const string TemplateId = "main_hero";

    public static CharacterObject Find() => MBObjectManager.Instance?.GetObject<CharacterObject>(TemplateId);

    /// <summary>
    /// Links <paramref name="character"/> to <paramref name="template"/> when it has no template link yet.
    /// </summary>
    /// <remarks>
    /// A new player's character is the ModuleData <c>main_hero</c>, which has no template link of its own. The engine
    /// saves only a character's hero and template link. After a load it refills a character from ModuleData by id, or
    /// from its template link (<c>SandBoxManager.InitializeCharactersAfterLoad</c>), and it unregisters a character
    /// that is still not ready. A joined character has a new id and no link, so without this it is empty and
    /// unregistered after every load. With the link, the engine refills it from <c>main_hero</c> as it refills every
    /// template-made hero, and a child made from it links to <c>main_hero</c> too.
    /// </remarks>
    public static void Link(CharacterObject character, CharacterObject template)
    {
        if (character == null || template == null || character == template) return;
        if (!character.IsOriginalCharacter) return;

        character._originCharacter = template;
    }
}
