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
    /// Vanilla sweeps visibility around the main party every frame through <see cref="MapVisibilityModel"/>;
    /// this recomputes one party on top of that model. A party inside a settlement stays hidden unless its
    /// leader carries a clan banner or it is assaulting the walls; the model alone would keep whatever
    /// visibility the party already had, which after a join is the server save's "everything visible".
    /// Settlements only refresh <c>IsInspected</c>: their <c>IsVisible</c> is spotting state.
    /// </summary>
    /// <param name="mainPartySeeingRange">The seeing range to use; 0 uses the main party's current range.</param>
    public static void UpdateVisibilityAndInspected(this PartyBase party, CampaignVec2 fromPosition, float mainPartySeeingRange = 0f)
    {
        var model = Campaign.Current?.Models?.MapVisibilityModel;
        if (party == null || model == null) return;

        float seeingRange = mainPartySeeingRange;
        if (seeingRange == 0f) seeingRange = MobileParty.MainParty?.SeeingRange ?? 0f;
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
        IsAssaultingWalls(mobileParty);

    private static bool IsAssaultingWalls(MobileParty mobileParty) =>
        mobileParty.MapEvent != null && mobileParty.MapEvent.IsSiegeAssault && mobileParty.Party.Side == BattleSideEnum.Attacker;
}
