using Common.Logging.Attributes;
using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#nullable enable

namespace GameInterface.Services.Towns.Messages;

/// <summary>
/// Used when the governor changes in a town.
/// </summary>
[BatchLogMessage]
public readonly struct TownGovernorChanged : ICommand
{
    public readonly Town Town;
    public readonly Hero? Governor;

    public TownGovernorChanged(Town town, Hero? governor)
    {
        Town = town;
        Governor = governor;
    }
}