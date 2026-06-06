using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages;

/// <summary>
/// Used when the TradeTaxAccumulated changes in a town.
/// </summary>
public readonly struct TownTradeTaxAccumulatedChanged : ICommand
{
    public readonly Town Town;
    public readonly int TradeTaxAccumulated;

    public TownTradeTaxAccumulatedChanged(Town town, int tradeTaxAccumulated)
    {
        Town = town;
        TradeTaxAccumulated = tradeTaxAccumulated;
    }
}