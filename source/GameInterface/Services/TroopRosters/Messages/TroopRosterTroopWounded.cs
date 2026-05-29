using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;

internal readonly struct TroopRosterTroopWounded : IEvent
{
    public readonly TroopRoster TroopRoster;
    public readonly CharacterObject Troop;
    public readonly int NumberToWound;

    public TroopRosterTroopWounded(TroopRoster troopRoster, CharacterObject troop, int numberToWound)
    {
        TroopRoster = troopRoster;
        Troop = troop;
        NumberToWound = numberToWound;
    }
}
