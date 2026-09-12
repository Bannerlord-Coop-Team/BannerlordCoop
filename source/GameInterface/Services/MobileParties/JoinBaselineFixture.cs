#if DEBUG
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties;

/// <summary>
/// Provides reversible campaign-collection control for DEBUG join tests.
/// </summary>
public static class JoinBaselineFixture
{
    public static void RemoveFromCampaignCollection(
        CampaignObjectManager campaignObjectManager,
        MobileParty mobileParty)
    {
        campaignObjectManager._mobileParties.Remove(mobileParty);
    }
}
#endif
