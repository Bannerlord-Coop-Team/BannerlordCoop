using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages;

/// <summary>
/// Used when the LastCapturedBy changes in a town.
/// </summary>
public readonly struct TownLastCapturedByChanged : ICommand
{
    public readonly Town Town;
    public readonly Clan Clan;

    public TownLastCapturedByChanged(Town town, Clan clan)
    {
        Town = town;
        Clan = clan;
    }
}