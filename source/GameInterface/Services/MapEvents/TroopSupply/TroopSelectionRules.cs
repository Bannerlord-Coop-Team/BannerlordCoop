using GameInterface.Configuration;
using GameInterface.Services.Hideouts;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>
/// What a player may bring into a battle fought with chosen squads (hideouts and the siege keep), and how the troop
/// picker's roster travels as entries.
/// </summary>
public interface ITroopSelectionRules
{
    /// <summary>The healthy troops of the party by character id; the player's hero counts when wounded entry is on.</summary>
    Dictionary<string, int> CountHealthy(MobileParty party, string heroCharacterId);

    /// <summary>
    /// The player's hero followed by the requested escorts, if every entry is healthy, the hero comes once and the
    /// escorts fit <paramref name="escortLimit"/>.
    /// </summary>
    bool TryAccept(IReadOnlyDictionary<string, int> healthy, string heroCharacterId, int escortLimit,
        IReadOnlyList<HideoutTroopSelectionEntry> requested, out HideoutTroopSelectionEntry[] accepted);

    /// <summary>The picker's chosen roster as entries, false if a character isn't registered.</summary>
    bool TryCreateEntries(TroopRoster selected, out HideoutTroopSelectionEntry[] entries);

    /// <summary>A roster holding only the hero, for a player with no escort place.</summary>
    TroopRoster CreateHeroOnlyRoster(Hero hero);

    /// <summary>Keeps only the chosen troops of a party's reserve, in the order they were chosen.</summary>
    void TrimReserve(List<TroopReserveEntry> entries, IEnumerable<HideoutTroopSelectionEntry> chosen);
}

/// <inheritdoc cref="ITroopSelectionRules"/>
public class TroopSelectionRules : ITroopSelectionRules
{
    private readonly IObjectManager objectManager;

    public TroopSelectionRules(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public Dictionary<string, int> CountHealthy(MobileParty party, string heroCharacterId)
    {
        var healthy = new Dictionary<string, int>();
        foreach (var element in party.MemberRoster.GetTroopRoster())
        {
            if (element.Character == null || !objectManager.TryGetId(element.Character, out var characterId))
                continue;

            var count = element.Number - element.WoundedNumber;
            if (characterId == heroCharacterId && ModConfigProvider.ModOptions.PlayerWoundedBattleEntry)
                count = element.Number;
            healthy[characterId] = count;
        }
        return healthy;
    }

    public bool TryAccept(IReadOnlyDictionary<string, int> healthy, string heroCharacterId, int escortLimit,
        IReadOnlyList<HideoutTroopSelectionEntry> requested, out HideoutTroopSelectionEntry[] accepted)
    {
        accepted = null;
        if (!healthy.TryGetValue(heroCharacterId, out var heroes) || heroes < 1)
            return false;

        var selection = new List<HideoutTroopSelectionEntry> { new(heroCharacterId, 1) };
        var selectedCounts = new Dictionary<string, int>();
        var escorts = 0;
        foreach (var entry in requested)
        {
            if (string.IsNullOrEmpty(entry.CharacterId) || entry.Count < 1)
                return false;
            if (entry.CharacterId == heroCharacterId)
            {
                if (entry.Count != 1) return false;
                continue;
            }

            selectedCounts.TryGetValue(entry.CharacterId, out var alreadySelected);
            if (!healthy.TryGetValue(entry.CharacterId, out var available) ||
                entry.Count > available - alreadySelected || entry.Count > escortLimit - escorts)
                return false;

            selectedCounts[entry.CharacterId] = alreadySelected + entry.Count;
            escorts += entry.Count;
            selection.Add(entry);
        }

        accepted = selection.ToArray();
        return true;
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

    public void TrimReserve(List<TroopReserveEntry> entries, IEnumerable<HideoutTroopSelectionEntry> chosen)
    {
        var available = entries.GroupBy(entry => entry.CharacterId)
            .ToDictionary(group => group.Key, group => new Queue<TroopReserveEntry>(group));
        var selected = new List<TroopReserveEntry>();
        foreach (var entry in chosen)
        {
            if (!available.TryGetValue(entry.CharacterId, out var queue)) continue;
            for (var count = 0; count < entry.Count && queue.Count > 0; count++)
                selected.Add(queue.Dequeue());
        }
        entries.Clear();
        entries.AddRange(selected);
    }
}
