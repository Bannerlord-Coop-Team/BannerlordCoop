using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages;

/// <summary>
/// Used when the loyalty changes in a town.
/// </summary>
public readonly struct TownLoyaltyChanged : ICommand
{
    public readonly Town Town;
    public readonly float Loyalty;

    public TownLoyaltyChanged(Town town, float loyalty)
    {
        Town = town;
        Loyalty = loyalty;
    }
}