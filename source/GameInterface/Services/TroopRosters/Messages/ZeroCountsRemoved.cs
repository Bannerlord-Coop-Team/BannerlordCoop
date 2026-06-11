using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;

internal readonly struct ZeroCountsRemoved : IEvent
{
    public readonly TroopRoster TroopRoster;

    public ZeroCountsRemoved(TroopRoster troopRoster)
    {
        TroopRoster = troopRoster;
    }
}
