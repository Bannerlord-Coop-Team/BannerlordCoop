using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Interfaces;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

internal interface IArtisanProductLordActions
{
    bool TryApply(Issue issue, Hero player, MobileParty party, ArtisanProductLordAction action);
}

internal sealed class ArtisanProductLordActions : IArtisanProductLordActions
{
    private readonly IArtisanProductTraits traits;

    public ArtisanProductLordActions(IArtisanProductTraits traits) => this.traits = traits;

    public bool TryApply(Issue issue, Hero player, MobileParty party, ArtisanProductLordAction action)
    {
        if (ModInformation.IsClient || issue == null || player?.Clan == null || party == null) return false;
        using (new MainHeroSubstitutionScope(player, party))
        using (traits.Enter(player))
        using (new IssueFinalizeAuthorityGuard())
        {
            if (action == ArtisanProductLordAction.Start)
            {
                if (!issue.IsOngoingWithoutQuest || !issue.IssueStayAliveConditions()) return false;
                if (!issue.CheckPreconditions(issue.IssueOwner, out _)) return false;
                if (!issue.LordSolutionCondition(out _) || player.Clan.Influence < issue.NeededInfluenceForLordSolution) return false;
                issue.StartIssueWithLordSolution();
                return true;
            }

            if (!issue.IsSolvingWithLordSolution) return false;
            switch (action)
            {
                case ArtisanProductLordAction.AcceptMerchantOffer:
                    issue.CompleteIssueWithLordSolutionWithAcceptCounterOffer();
                    return true;
                case ArtisanProductLordAction.RefuseMerchantOffer:
                    issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer();
                    return true;
                default:
                    return false;
            }
        }
    }
}
