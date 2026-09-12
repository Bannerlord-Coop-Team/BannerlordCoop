using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Unstuck;

/// <summary>
/// The local player asked to be forced out of stuck map states (coop.debug.mobileparty.unstuck).
/// Forwarded to the server as <see cref="NetworkRequestPlayerUnstuck"/>.
/// </summary>
internal readonly struct PlayerUnstuckRequested : IEvent
{
    public MobileParty Party { get; }

    public PlayerUnstuckRequested(MobileParty party)
    {
        Party = party;
    }
}
