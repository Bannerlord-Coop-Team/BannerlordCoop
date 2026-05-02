using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages;

/// <summary>
/// Used when the prosperity changes in a town.
/// </summary>
public readonly struct TownProsperityChanged : ICommand
{
    public readonly Town Town;
    public readonly float Prosperity;

    public TownProsperityChanged(Town town, float prosperity)
    {
        Town = town;
        Prosperity = prosperity;
    }
}