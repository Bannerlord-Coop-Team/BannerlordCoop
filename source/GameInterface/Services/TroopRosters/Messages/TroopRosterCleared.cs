using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;

internal readonly struct TroopRosterCleared : IEvent
{
    public readonly TroopRoster TroopRoster;

    public TroopRosterCleared(TroopRoster troopRoster)
    {
        TroopRoster = troopRoster;
    }
}
