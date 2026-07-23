using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Published when <see cref="TroopRoster.SetElementNumber"/> is called on the authority.
/// The element is identified by its <see cref="Character"/> to stay robust to roster ordering.
/// </summary>
internal readonly struct ElementNumberSet : IEvent
{
    public readonly TroopRoster TroopRoster;
    public readonly CharacterObject Character;
    public readonly int Number;

    public ElementNumberSet(TroopRoster troopRoster, CharacterObject character, int number)
    {
        TroopRoster = troopRoster;
        Character = character;
        Number = number;
    }
}
