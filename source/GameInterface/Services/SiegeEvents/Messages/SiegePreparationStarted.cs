using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// A siege started preparing against a settlement on the server; players inside need their
/// preparation menu.
/// </summary>
public readonly struct SiegePreparationStarted : IEvent
{
    public readonly MobileParty BesiegerParty;
    public readonly Settlement Settlement;

    public SiegePreparationStarted(MobileParty besiegerParty, Settlement settlement)
    {
        BesiegerParty = besiegerParty;
        Settlement = settlement;
    }
}
