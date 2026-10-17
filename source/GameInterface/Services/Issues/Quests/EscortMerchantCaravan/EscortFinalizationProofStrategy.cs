using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan;

internal enum EscortBranch : byte
{
    AttackCaravan,
}

internal sealed class EscortFinalizationProofStrategy : IFinalizationProofStrategy
{
    public bool TryRunBranch(QuestBase questBase, byte proof)
    {
        if (!(questBase is Quest quest) || !quest.IsOngoing ||
            proof != (byte)EscortBranch.AttackCaravan || quest._questCaravanMobileParty?.IsActive != true)
            return false;

        BeHostileAction.ApplyEncounterHostileAction(PartyBase.MainParty, quest._questCaravanMobileParty.Party);
        if (quest.IsOngoing) quest.FailByPlayerHostileConsequences();
        return true;
    }
}
