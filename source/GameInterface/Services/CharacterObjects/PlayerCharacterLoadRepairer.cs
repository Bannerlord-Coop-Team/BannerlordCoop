using Common.Logging;
using GameInterface.Services.Heroes.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CharacterObjects;

/// <summary>
/// Repairs joined players' characters around the engine's after-load refill (#4675).
/// </summary>
internal interface IPlayerCharacterLoadRepairer
{
    /// <summary>
    /// Links each joined player's character that an older save left without a template link, and moves a child made
    /// from one onto <paramref name="template"/>. Returns how many it linked.
    /// </summary>
    int RepairLoadedPlayers(IReadOnlyCollection<Hero> heroes, CharacterObject template);

    /// <summary>
    /// Gives each hero character linked to <paramref name="template"/> its hero's culture, and returns how many it changed.
    /// </summary>
    int RestoreHeroCultures(IReadOnlyCollection<Hero> heroes, CharacterObject template);
}

/// <summary>
/// Fixes the template link and culture of characters the engine refills from <c>main_hero</c>.
/// </summary>
internal class PlayerCharacterLoadRepairer : IPlayerCharacterLoadRepairer
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerCharacterLoadRepairer>();

    /// <inheritdoc/>
    /// <remarks>
    /// Runs before the refill, which then fills the linked characters. Only joined-player ids are linked, because a
    /// hollow lord can also hold a not-ready character under a ModuleData id.
    /// </remarks>
    public int RepairLoadedPlayers(IReadOnlyCollection<Hero> heroes, CharacterObject template)
    {
        if (template == null) return 0;

        var linked = new HashSet<CharacterObject>();
        foreach (Hero hero in heroes)
        {
            CharacterObject character = hero.CharacterObject;
            if (character == null || character == template || character.IsReady) continue;
            if (!character.IsOriginalCharacter || !IsJoinedPlayerId(character.StringId)) continue;

            character._originCharacter = template;
            linked.Add(character);
        }

        if (linked.Count == 0) return 0;

        // CreateFrom gives a child of a linked parent the parent's origin, so the child follows it to the template
        foreach (Hero hero in heroes)
        {
            CharacterObject character = hero.CharacterObject;
            if (character?._originCharacter != null && linked.Contains(character._originCharacter))
                character._originCharacter = template;
        }

        Logger.Information(
            "Linked {Count} joined player character(s) without a template link to {TemplateId} so the load refills them (#4675)",
            linked.Count,
            PlayerCharacterTemplate.TemplateId);

        return linked.Count;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The refill copies <c>main_hero</c>'s culture into the character's own field, which an agent's
    /// <c>BasicCharacterObject.Culture</c> reads instead of the hero's culture.
    /// </remarks>
    public int RestoreHeroCultures(IReadOnlyCollection<Hero> heroes, CharacterObject template)
    {
        if (template == null) return 0;

        int restored = 0;
        foreach (Hero hero in heroes)
        {
            CharacterObject character = hero.CharacterObject;
            if (character == null || character._originCharacter != template) continue;
            if (hero.Culture == null || character._culture == hero.Culture) continue;

            character._culture = hero.Culture;
            restored++;
        }

        if (restored > 0)
        {
            Logger.Information(
                "Gave {Count} character(s) linked to {TemplateId} their hero's culture after the load refill (#4675)",
                restored,
                PlayerCharacterTemplate.TemplateId);
        }

        return restored;
    }

    /// <summary>
    /// True for the ids <see cref="HeroInterface.JoinedPlayerIdPrefix"/> gives, the prefix alone or followed by digits.
    /// </summary>
    internal static bool IsJoinedPlayerId(string stringId)
    {
        if (stringId == null || !stringId.StartsWith(HeroInterface.JoinedPlayerIdPrefix, StringComparison.Ordinal)) return false;

        for (int i = HeroInterface.JoinedPlayerIdPrefix.Length; i < stringId.Length; i++)
        {
            if (stringId[i] < '0' || stringId[i] > '9') return false;
        }

        return true;
    }
}
