using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Alleys;

/// <summary>
/// Converts an alley garrison between a live <see cref="TroopRoster"/> and the id-keyed
/// <see cref="TroopRosterElementData"/> snapshot used for storage and networking.
/// </summary>
internal static class AlleyGarrisonData
{
    public static TroopRosterElementData[] ToData(TroopRoster roster, IObjectManager objectManager)
    {
        var list = new List<TroopRosterElementData>();
        if (roster == null) return list.ToArray();

        foreach (var element in roster.GetTroopRoster())
        {
            if (!objectManager.TryGetIdWithLogging(element.Character, out var characterId)) continue;
            list.Add(new TroopRosterElementData(characterId, element.Number, element.WoundedNumber, element.Xp));
        }
        return list.ToArray();
    }

    public static TroopRoster FromData(TroopRosterElementData[] data, IObjectManager objectManager)
    {
        var roster = TroopRoster.CreateDummyTroopRoster();
        if (data == null) return roster;

        foreach (var d in data)
        {
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(d.CharacterId, out var character)) continue;
            roster.AddToCounts(character, d.Number, false, d.WoundedNumber, d.Xp, true, -1);
        }
        return roster;
    }
}
