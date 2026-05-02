using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages;

/// <summary>
/// Used when the security changes in a town.
/// </summary>
public readonly struct TownSecurityChanged : ICommand
{
    public readonly Town Town;
    public readonly float Security;

    public TownSecurityChanged(Town town, float security)
    {
        Town = town;
        Security = security;
    }
}