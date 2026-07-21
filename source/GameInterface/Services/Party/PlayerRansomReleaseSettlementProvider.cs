using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Party;

internal interface IPlayerRansomReleaseSettlementProvider
{
    Settlement GetReleaseSettlement(PartyBase sellingParty, Hero playerHero);
}

internal class PlayerRansomReleaseSettlementProvider : IPlayerRansomReleaseSettlementProvider
{
    private readonly System.Func<Settlement, MobileParty.NavigationType, System.Func<Settlement, bool>, Settlement>
        findNearestToSettlement;
    private readonly System.Func<CampaignVec2, System.Func<Settlement, bool>, Settlement> findNearestToPoint;

    public PlayerRansomReleaseSettlementProvider()
        : this(
            SettlementHelper.FindNearestSettlementToSettlement,
            (position, condition) => SettlementHelper.FindNearestSettlementToPoint(position, condition))
    {
    }

    internal PlayerRansomReleaseSettlementProvider(
        System.Func<Settlement, MobileParty.NavigationType, System.Func<Settlement, bool>, Settlement>
            findNearestToSettlement,
        System.Func<CampaignVec2, System.Func<Settlement, bool>, Settlement> findNearestToPoint)
    {
        if (findNearestToSettlement == null)
            throw new System.ArgumentNullException(nameof(findNearestToSettlement));
        if (findNearestToPoint == null)
            throw new System.ArgumentNullException(nameof(findNearestToPoint));

        this.findNearestToSettlement = findNearestToSettlement;
        this.findNearestToPoint = findNearestToPoint;
    }

    public Settlement GetReleaseSettlement(PartyBase sellingParty, Hero playerHero)
    {
        if (sellingParty == null) throw new System.ArgumentNullException(nameof(sellingParty));
        if (playerHero == null) throw new System.ArgumentNullException(nameof(playerHero));

        var ransomSettlement = sellingParty.MobileParty?.CurrentSettlement ?? sellingParty.Settlement;
        var condition = new System.Func<Settlement, bool>(settlement =>
            IsEligible(settlement, ransomSettlement, playerHero.MapFaction));

        var releaseSettlement = FindNearest(sellingParty, ransomSettlement, condition);
        if (releaseSettlement != null)
            return releaseSettlement;

        // Preserve the ransom flow when the campaign has no neutral or allied destination.
        var fallbackCondition = new System.Func<Settlement, bool>(settlement =>
            IsSettlementDestination(settlement) && settlement != ransomSettlement);
        releaseSettlement = FindNearest(sellingParty, ransomSettlement, fallbackCondition);
        if (releaseSettlement != null)
            return releaseSettlement;

        if (ransomSettlement != null)
            return ransomSettlement;

        throw new System.InvalidOperationException(
            $"No release settlement exists for player hero '{playerHero.StringId}'.");
    }

    private Settlement FindNearest(
        PartyBase sellingParty,
        Settlement ransomSettlement,
        System.Func<Settlement, bool> condition)
    {
        if (ransomSettlement != null)
        {
            return findNearestToSettlement(
                ransomSettlement,
                MobileParty.NavigationType.Default,
                condition);
        }

        return findNearestToPoint(sellingParty.Position, condition);
    }

    private static bool IsEligible(
        Settlement settlement,
        Settlement ransomSettlement,
        IFaction playerFaction)
    {
        if (settlement == ransomSettlement || settlement.IsUnderSiege ||
            !IsSettlementDestination(settlement))
            return false;

        var settlementFaction = settlement.MapFaction;
        return playerFaction == null || settlementFaction == null ||
            !FactionManager.IsAtWarAgainstFaction(playerFaction, settlementFaction);
    }

    private static bool IsSettlementDestination(Settlement settlement) =>
        settlement != null && (settlement.IsTown || settlement.IsCastle || settlement.IsVillage);
}
