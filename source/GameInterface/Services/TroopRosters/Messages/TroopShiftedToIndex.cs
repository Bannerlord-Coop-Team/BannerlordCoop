using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Published when <see cref="TroopRoster.ShiftTroopToIndex"/> is called on the authority.
/// </summary>
internal readonly struct TroopShiftedToIndex : IEvent
{
    public readonly TroopRoster TroopRoster;
    public readonly int TroopIndex;
    public readonly int TargetIndex;

    public TroopShiftedToIndex(TroopRoster troopRoster, int troopIndex, int targetIndex)
    {
        TroopRoster = troopRoster;
        TroopIndex = troopIndex;
        TargetIndex = targetIndex;
    }
}
