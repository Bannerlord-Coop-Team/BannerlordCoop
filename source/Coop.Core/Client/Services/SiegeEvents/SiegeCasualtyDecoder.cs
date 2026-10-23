using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace Coop.Core.Client.Services.SiegeEvents;

/// <summary>
/// Turns the troop ids and counts of a break in or break out reply back into the casualty roster the debrief shows.
/// </summary>
internal interface ISiegeCasualtyDecoder
{
    /// <summary>
    /// Null arrays read as empty, since protobuf sends an empty array as null (an army leader whose own party lost nobody).
    /// Returns false when the arrays don't match or a troop id is unknown.
    /// </summary>
    bool TryDecode(uint[] characterIds, int[] counts, out TroopRoster casualties);
}

/// <inheritdoc cref="ISiegeCasualtyDecoder"/>
internal class SiegeCasualtyDecoder : ISiegeCasualtyDecoder
{
    private readonly IObjectManager objectManager;

    public SiegeCasualtyDecoder(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public bool TryDecode(uint[] characterIds, int[] counts, out TroopRoster casualties)
    {
        casualties = null;
        var ids = characterIds ?? Array.Empty<uint>();
        var numbers = counts ?? Array.Empty<int>();
        if (ids.Length != numbers.Length) return false;

        var roster = TroopRoster.CreateDummyTroopRoster();
        for (int index = 0; index < ids.Length; index++)
        {
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(ids[index], out var character)) return false;
            roster.AddToCounts(character, numbers[index]);
        }
        casualties = roster;
        return true;
    }
}
