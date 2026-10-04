using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

using Quest = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

public interface IHeadmanHerdDeliveryInterface
{
    bool CanDeliver(Quest quest, MobileParty party);
    bool CanReject(Quest quest, MobileParty party);
    void Deliver(Quest quest);
    void Reject(Quest quest);
}

internal sealed class HeadmanHerdDeliveryInterface : IHeadmanHerdDeliveryInterface
{
    private readonly IHeadmanHerdQuestAuthority authority;

    public HeadmanHerdDeliveryInterface(IHeadmanHerdQuestAuthority authority)
    {
        this.authority = authority;
    }

    public bool CanDeliver(Quest quest, MobileParty party)
    {
        if (!CanReject(quest, party)) return false;
        var count = 0;
        foreach (var element in party.ItemRoster)
        {
            if (element.EquipmentElement.Item == quest._herdTypeToDeliver)
                count += element.Amount;
        }
        return count >= quest._animalCountToDeliver;
    }

    public bool CanReject(Quest quest, MobileParty party)
    {
        return quest != null && quest.IsOngoing && party != null
            && quest._targetSettlement != null && party.CurrentSettlement == quest._targetSettlement
            && quest._targetHero != null && quest._targetHero.CurrentSettlement == quest._targetSettlement;
    }

    public void Deliver(Quest quest)
    {
        if (!authority.TryEnter(quest.QuestGiver, out var scope))
            throw new InvalidOperationException("Deliver the Herd owner is unavailable");
        using (scope)
            quest.DeliverHerdOnConsequence();
    }

    public void Reject(Quest quest)
    {
        if (!authority.TryEnter(quest.QuestGiver, out var scope))
            throw new InvalidOperationException("Deliver the Herd owner is unavailable");
        // Rejection adds crime after finalization, so the owner context must outlive it.
        using (scope)
            quest.DeliverHerdRejectOnConsequence();
    }
}
