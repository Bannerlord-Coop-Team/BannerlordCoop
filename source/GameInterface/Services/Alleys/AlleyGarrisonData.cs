using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Alleys;

/// <summary>
/// Converts an alley garrison between live, network, and persistent representations.
/// </summary>
internal static class AlleyGarrisonData
{
    public static TroopRosterElementData[] ToData(TroopRoster roster, IObjectManager objectManager)
    {
        var list = new List<TroopRosterElementData>();
        if (roster == null) return list.ToArray();

        foreach (var element in roster.GetTroopRoster())
        {
            if (!objectManager.TryGetHandleWithLogging(element.Character, out var characterId)) continue;
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

    public static TroopRoster FromData(AlleyRosterElementData[] data, IObjectManager objectManager)
    {
        var roster = TroopRoster.CreateDummyTroopRoster();
        if (data == null) return roster;

        foreach (var element in data)
        {
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(element.CharacterId, out var character)) continue;
            roster.AddToCounts(
                character,
                element.Number,
                false,
                element.WoundedNumber,
                element.Xp,
                true,
                -1);
        }
        return roster;
    }

    public static AlleyRosterElementData[] ToStorageData(
        TroopRosterElementData[] data,
        IObjectManager objectManager)
    {
        var result = new List<AlleyRosterElementData>();
        if (data == null) return result.ToArray();

        foreach (var element in data)
        {
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(element.CharacterId, out var character) ||
                !objectManager.TryGetIdWithLogging(character, out var characterId))
            {
                continue;
            }

            result.Add(new AlleyRosterElementData(
                characterId,
                element.Number,
                element.WoundedNumber,
                element.Xp));
        }
        return result.ToArray();
    }

    public static TroopRosterElementData[] ToNetworkData(
        AlleyRosterElementData[] data,
        IObjectManager objectManager)
    {
        var result = new List<TroopRosterElementData>();
        if (data == null) return result.ToArray();

        foreach (var element in data)
        {
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(element.CharacterId, out var character) ||
                !objectManager.TryGetHandleWithLogging(character, out var characterHandle))
            {
                continue;
            }

            result.Add(new TroopRosterElementData(
                characterHandle,
                element.Number,
                element.WoundedNumber,
                element.Xp));
        }
        return result.ToArray();
    }
}
