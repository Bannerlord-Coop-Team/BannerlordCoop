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

    /// <summary>Looks up the player template, or null before ModuleData is loaded.</summary>
    public static CharacterObject Find() => MBObjectManager.Instance?.GetObject<CharacterObject>(TemplateId);

    /// <summary>
    /// Links <paramref name="character"/> to <paramref name="template"/> when it has no template link yet.
    /// </summary>
    /// <remarks>
    /// A save keeps only a character's hero and template link, so without a link the after-load refill leaves a
    /// joined character empty and unregistered.
    /// </remarks>
    public static void Link(CharacterObject character, CharacterObject template)
    {
        if (character == null || template == null || character == template) return;
        if (!character.IsOriginalCharacter) return;

        character._originCharacter = template;
    }
}
