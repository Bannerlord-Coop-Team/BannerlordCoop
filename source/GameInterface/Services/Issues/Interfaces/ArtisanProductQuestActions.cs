using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using Common;
using GameInterface.Services.Issues.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using System.Linq;

namespace GameInterface.Services.Issues.Interfaces;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;
using Quest = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

public interface IArtisanProductQuestActions
{
    bool TryApply(Issue issue, Hero player, MobileParty party, ArtisanProductQuestAction action, int expectedDelivered);
    void CancelForPlayer(string controllerId);
    void CancelInvalidLoadedIssues();
}

internal sealed class ArtisanProductQuestActions : IArtisanProductQuestActions
{
    private readonly IArtisanProductTraits traits;
    private readonly IIssueOwnershipRegistry ownership;

    public ArtisanProductQuestActions(IArtisanProductTraits traits, IIssueOwnershipRegistry ownership)
    {
        this.traits = traits;
        this.ownership = ownership;
    }

    public void CancelForPlayer(string controllerId)
    {
        if (ModInformation.IsClient) return;
        foreach (var quest in Campaign.Current.QuestManager.Quests.OfType<Quest>().ToArray())
        {
            if (!quest.IsOngoing || !ownership.TryGetOwnerControllerId(quest.QuestGiver, out var owner) ||
                owner != controllerId) continue;
            quest.CompleteQuestWithCancel(new TextObject("{=bYdhYidf}The quest was canceled because your clan leader, who made the original agreement, is no longer head of the clan.\""));
        }
    }

    public void CancelInvalidLoadedIssues()
    {
        if (ModInformation.IsClient) return;
        foreach (var issue in Campaign.Current.IssueManager.Issues.Values.OfType<Issue>().ToArray())
            if (issue.IssueOwner.IsNotable && issue.IssueOwner.CurrentSettlement == null)
                issue.CompleteIssueWithCancel();
    }

    public bool TryApply(Issue issue, Hero player, MobileParty party, ArtisanProductQuestAction action, int expectedDelivered)
    {
        if (ModInformation.IsClient) return false;
        if (issue?.IssueQuest is not Quest quest || !quest.IsOngoing || player == null || party == null) return false;
        if (quest._deliveredRawGoods != expectedDelivered) return false;

        var available = party.ItemRoster.GetItemNumber(quest._rawMaterialsToBeDelivered);
        if (!CanApply(action, quest._deliveredRawGoods, quest._amountOfRawGoodsToBeDelivered,
            available, quest._counterOfferRefused)) return false;
        if (IsDelivery(action) && party.CurrentSettlement != quest._targetSettlement) return false;

        using (new MainHeroSubstitutionScope(player, party))
        using (traits.Enter(player))
        using (new IssueFinalizeAuthorityGuard())
        {
            switch (action)
            {
                case ArtisanProductQuestAction.DeliverPartially:
                    quest.DeliverItemsPartiallyOnConsequence();
                    break;
                case ArtisanProductQuestAction.DeliverFully:
                    quest.DeliverItemsFullyOnConsequence();
                    break;
                case ArtisanProductQuestAction.RefuseDelivery:
                    quest.DeliverItemsRejectOnConsequence();
                    break;
                case ArtisanProductQuestAction.AcceptMerchantOffer:
                    quest.QuestFailedWithRefusal();
                    break;
                case ArtisanProductQuestAction.RefuseMerchantOffer:
                    quest.RefuseCounterOfferConsequences();
                    break;
                case ArtisanProductQuestAction.MerchantOfferShown:
                    quest._counterOfferGiven = true;
                    break;
            }
        }
        return true;
    }

    internal static bool CanApply(ArtisanProductQuestAction action, int delivered, int required,
        int available, bool merchantRefused)
    {
        if (delivered < 0 || required <= delivered || available < 0) return false;

        switch (action)
        {
            case ArtisanProductQuestAction.DeliverPartially:
                return available > 0 && available < required - delivered;
            case ArtisanProductQuestAction.DeliverFully:
                return available >= required - delivered;
            case ArtisanProductQuestAction.RefuseDelivery:
                return true;
            case ArtisanProductQuestAction.AcceptMerchantOffer:
            case ArtisanProductQuestAction.RefuseMerchantOffer:
                return !merchantRefused;
            case ArtisanProductQuestAction.MerchantOfferShown:
                return true;
            default:
                return false;
        }
    }

    private static bool IsDelivery(ArtisanProductQuestAction action)
        => action == ArtisanProductQuestAction.DeliverPartially || action == ArtisanProductQuestAction.DeliverFully ||
            action == ArtisanProductQuestAction.RefuseDelivery;
}
