using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Published when <see cref="TroopRoster.AddNewElement"/> is called on the authority.
/// </summary>
internal readonly struct NewElementAdded : IEvent
{
    public readonly TroopRoster TroopRoster;
    public readonly CharacterObject Character;
    public readonly int InsertionIndex;

    public NewElementAdded(TroopRoster troopRoster, CharacterObject character, int insertionIndex)
    {
        TroopRoster = troopRoster;
        Character = character;
        InsertionIndex = insertionIndex;
    }
}
