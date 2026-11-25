using GameInterface.Services.Hideouts;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>How a squad held as a troop roster travels as the entries a chosen-squad battle sends and stores.</summary>
public interface ITroopSelectionEntries
{
    /// <summary>The roster as entries, false if a character isn't registered.</summary>
    bool TryCreateEntries(TroopRoster selected, out HideoutTroopSelectionEntry[] entries);

    /// <summary>A roster holding only the hero, for a player with no escort place.</summary>
    TroopRoster CreateHeroOnlyRoster(Hero hero);
}

/// <inheritdoc cref="ITroopSelectionEntries"/>
public class TroopSelectionEntries : ITroopSelectionEntries
{
    private readonly IObjectManager objectManager;

    public TroopSelectionEntries(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public bool TryCreateEntries(TroopRoster selected, out HideoutTroopSelectionEntry[] entries)
    {
        entries = null;
        var troops = new List<HideoutTroopSelectionEntry>();
        foreach (var element in selected.GetTroopRoster())
        {
            if (element.Number <= 0) continue;
            if (!objectManager.TryGetIdWithLogging(element.Character, out var characterId)) return false;
            troops.Add(new HideoutTroopSelectionEntry(characterId, element.Number));
        }

        entries = troops.ToArray();
        return true;
    }

    public TroopRoster CreateHeroOnlyRoster(Hero hero)
    {
        var heroOnly = TroopRoster.CreateDummyTroopRoster();
        heroOnly.AddToCounts(hero.CharacterObject, 1);
        return heroOnly;
    }
}
