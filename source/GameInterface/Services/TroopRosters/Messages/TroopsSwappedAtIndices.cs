using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Published when <see cref="TroopRoster.SwapTroopsAtIndices"/> is called on the authority.
/// </summary>
internal readonly struct TroopsSwappedAtIndices : IEvent
{
    public readonly TroopRoster TroopRoster;
    public readonly int FirstIndex;
    public readonly int SecondIndex;

    public TroopsSwappedAtIndices(TroopRoster troopRoster, int firstIndex, int secondIndex)
    {
        TroopRoster = troopRoster;
        FirstIndex = firstIndex;
        SecondIndex = secondIndex;
    }
}
