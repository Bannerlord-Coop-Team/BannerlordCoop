using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// [Server] Raised AFTER a party's "occupancy" changed — it entered or left a map event
/// (<c>PartyBase.MapEventSide</c>) or a settlement (<c>MobileParty.CurrentSettlement</c>). Published post-set,
/// so the party's <c>MapEvent</c>/<c>CurrentSettlement</c> already reflect the new state. Fires for every party;
/// consumers filter to connected players.
/// </summary>
public readonly struct PartyOccupancyChanged : IEvent
{
    public readonly MobileParty MobileParty;

    public PartyOccupancyChanged(MobileParty mobileParty)
    {
        MobileParty = mobileParty;
    }
}
