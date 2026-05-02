using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Control;

/// <summary>
/// Event fired when the local main player has changed.
/// </summary>
public readonly struct MainPartyChanged : IEvent
{
    public readonly MobileParty NewParty;

    public MainPartyChanged(MobileParty newParty)
    {
        NewParty = newParty;
    }
}
