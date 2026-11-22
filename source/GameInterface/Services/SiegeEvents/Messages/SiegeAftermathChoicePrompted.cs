using Common.Messaging;
using TaleWorlds.CampaignSystem;
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
    public readonly Clan PreviousOwnerClan;

    public SiegeAftermathChoicePrompted(MobileParty leaderParty, Settlement settlement, Clan previousOwnerClan)
    {
        LeaderParty = leaderParty;
        Settlement = settlement;
        PreviousOwnerClan = previousOwnerClan;
    }
}
