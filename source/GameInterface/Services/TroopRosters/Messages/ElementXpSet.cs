using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Published when <see cref="TroopRoster.SetElementXp"/> is called on the authority.
/// The element is identified by its <see cref="Character"/> to stay robust to roster ordering.
/// </summary>
internal readonly struct ElementXpSet : IEvent
{
    public readonly TroopRoster TroopRoster;
    public readonly int Index;
    public readonly int Number;

    public ElementXpSet(TroopRoster troopRoster, int index, int number)
    {
        TroopRoster = troopRoster;
        Index = index;
        Number = number;
    }
}
