using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Published when <see cref="TroopRoster.AddToCountsAtIndex"/> is called on the authority.
/// </summary>
/// <remarks>
/// The element is identified by its <see cref="Character"/> rather than its raw index so the
/// change can be applied even if the client roster ordering differs from the server's.
/// </remarks>
internal readonly struct CountsAtIndexAdded : IEvent
{
    public readonly TroopRoster TroopRoster;
    public readonly int Index;
    public readonly int CountChange;
    public readonly int WoundedCountChange;
    public readonly int XpChange;
    public readonly bool RemoveDepleted;

    public CountsAtIndexAdded(
        TroopRoster troopRoster,
        int index,
        int countChange,
        int woundedCountChange,
        int xpChange,
        bool removeDepleted)
    {
        TroopRoster = troopRoster;
        Index = index;
        CountChange = countChange;
        WoundedCountChange = woundedCountChange;
        XpChange = xpChange;
        RemoveDepleted = removeDepleted;
    }
}
