using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Extensions;

public static class PartyBaseVisibilityExtensions
{
    /// <summary>
    /// Recomputes one party's fog of war state from <paramref name="fromPosition"/>.
    /// v1.5 removed <c>PartyBase.UpdateVisibilityAndInspected</c>: visibility is now swept around the
    /// main party every frame through <see cref="MapVisibilityModel"/>. This keeps the v1.4 entry point
    /// on top of that model. A party inside a settlement stays hidden unless its leader carries a clan
    /// banner or it is assaulting the walls, as v1.4 decided; the v1.5 model would keep whatever
    /// visibility the party already had, which after a join is the server save's "everything visible".
    /// Settlements only refresh <c>IsInspected</c>: their <c>IsVisible</c> is spotting state in v1.5.
    /// </summary>
    public static void UpdateVisibilityAndInspected(this PartyBase party, CampaignVec2 fromPosition, float mainPartySeeingRange = 0f)
    {
        var model = Campaign.Current?.Models?.MapVisibilityModel;
        if (party == null || model == null) return;

        float seeingRange = mainPartySeeingRange == 0f ? MobileParty.MainParty?.SeeingRange ?? 0f : mainPartySeeingRange;
        if (seeingRange <= 0f) return;

        Vec2[] points = { fromPosition.ToVec2() };

        if (party.IsSettlement)
        {
            model.GetSettlementInspectedState(party.Settlement, points, seeingRange, out bool settlementInspected, out _);
            party.Settlement.IsInspected = settlementInspected;
            return;
        }

        MobileParty mobileParty = party.MobileParty;
        if (mobileParty == null) return;

        bool isVisible = false;
        bool isInspected = false;
        if (mobileParty.IsActive && IsVisibleFromOutside(mobileParty))
        {
            // Like the vanilla sweep, leave a party whose visibility the model treats as persistent
            // (true sight, the player's captor, garrisons, militia) as it is.
            if (model.IsVisibilityPersistent(mobileParty)) return;

            model.GetMobilePartyVisibilityAndInspectedState(mobileParty, points, seeingRange, out isVisible, out isInspected);
        }

        mobileParty.IsVisible = isVisible;
        mobileParty.IsInspected = isInspected;
    }

    private static bool IsVisibleFromOutside(MobileParty mobileParty) =>
        Campaign.Current.TrueSight ||
        mobileParty.CurrentSettlement == null ||
        mobileParty.LeaderHero?.ClanBanner != null ||
        (mobileParty.MapEvent != null && mobileParty.MapEvent.IsSiegeAssault && mobileParty.Party.Side == BattleSideEnum.Attacker);
}
