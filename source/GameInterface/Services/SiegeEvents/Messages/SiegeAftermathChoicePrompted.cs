using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// The server parked a player-led siege aftermath and needs that leader's client to open the
/// Devastate/Pillage/Show Mercy menu.
/// </summary>
public readonly struct SiegeAftermathChoicePrompted : IEvent
{
    public readonly MobileParty LeaderParty;
    public readonly Settlement Settlement;

    public SiegeAftermathChoicePrompted(MobileParty leaderParty, Settlement settlement)
    {
        LeaderParty = leaderParty;
        Settlement = settlement;
    }
}
