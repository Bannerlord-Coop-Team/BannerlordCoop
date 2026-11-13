using System;
using System.Collections.Generic;
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

    /// <summary>The id prefix the host gives every joined player's hero, and so its character.</summary>
    public const string JoinedPlayerIdPrefix = "Player";

    /// <summary>
    /// True for <c>Player</c> or <c>Player</c> plus digits, the ids <c>FindNextUniqueStringId</c> returns for that prefix.
    /// </summary>
    public static bool IsJoinedPlayerId(string stringId)
    {
        if (stringId == null || !stringId.StartsWith(JoinedPlayerIdPrefix, StringComparison.Ordinal)) return false;

        for (int i = JoinedPlayerIdPrefix.Length; i < stringId.Length; i++)
        {
            if (stringId[i] < '0' || stringId[i] > '9') return false;
        }

        return true;
    }

    /// <summary>
    /// Links each joined player's character that a save made before <see cref="Link"/> left without a template link,
    /// and moves the template of a child made from one to <paramref name="template"/>. Returns how many it linked.
    /// </summary>
    /// <remarks>
    /// Runs before the engine's after-load refill, so the refill then fills these characters. <c>IsReady</c> is not
    /// saved, so at that point only a character the ModuleData pass reached in this load is ready. A hollow lord can
    /// hold a not-ready character under a ModuleData id, so the id check matters: only joined-player ids are linked,
    /// and the engine unregisters a hollow lord unless another mod repairs it first. A child's origin becomes the
    /// template because <c>CreateFrom</c> gives a child of a linked parent the parent's origin. A character that an
    /// earlier load already unregistered was saved that way, so it is filled but stays unregistered.
    /// </remarks>
    public static int RepairLoadedPlayers(IReadOnlyCollection<Hero> heroes, CharacterObject template)
    {
        if (heroes == null || template == null) return 0;

        var linked = new HashSet<CharacterObject>();
        foreach (Hero hero in heroes)
        {
            CharacterObject character = hero?.CharacterObject;
            if (character == null || character == template || character.IsReady) continue;
            if (!character.IsOriginalCharacter || !IsJoinedPlayerId(character.StringId)) continue;

            character._originCharacter = template;
            linked.Add(character);
        }

        if (linked.Count == 0) return 0;

        foreach (Hero hero in heroes)
        {
            CharacterObject character = hero?.CharacterObject;
            if (character?._originCharacter != null && linked.Contains(character._originCharacter))
            {
                character._originCharacter = template;
            }
        }

        return linked.Count;
    }

    /// <summary>
    /// Gives each hero character linked to <paramref name="template"/> its hero's culture as its own culture, and
    /// returns how many it changed.
    /// </summary>
    /// <remarks>
    /// The after-load refill copies the template's culture into the character's own field, so a joined player's
    /// character holds <c>main_hero</c>'s ModuleData culture. <c>CharacterObject.Culture</c> answers with the hero's
    /// culture, but code that holds the character as a <c>BasicCharacterObject</c> (an agent's <c>Character</c>)
    /// reads the field. Coop's culture getter patch fills only an empty field, so it does not correct this. Runs
    /// after the refill on every load. A child of the host's hero is linked to <c>main_hero</c> too, and gets the
    /// same correction.
    /// </remarks>
    public static int RestoreHeroCultures(IReadOnlyCollection<Hero> heroes, CharacterObject template)
    {
        if (heroes == null || template == null) return 0;

        int restored = 0;
        foreach (Hero hero in heroes)
        {
            CharacterObject character = hero?.CharacterObject;
            if (character == null || character._originCharacter != template) continue;
            if (hero.Culture == null || character._culture == hero.Culture) continue;

            character._culture = hero.Culture;
            restored++;
        }

        return restored;
    }
}
