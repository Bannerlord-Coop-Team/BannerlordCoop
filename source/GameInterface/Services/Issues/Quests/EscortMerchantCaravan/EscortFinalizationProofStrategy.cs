using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using GameInterface.Services.Heroes.Patches;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Actions;
using Quest = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan;

internal enum EscortBranch : byte
{
    AttackCaravan,
}

internal sealed class EscortFinalizationProofStrategy : IFinalizationProofStrategy
{
    private readonly IEscortOwnerParty parties;

    public EscortFinalizationProofStrategy(IEscortOwnerParty parties)
    {
        this.parties = parties;
    }

    public bool TryRunBranch(QuestBase questBase, byte proof)
    {
        if (!(questBase is Quest quest) || !quest.IsOngoing ||
            proof != (byte)EscortBranch.AttackCaravan || quest._questCaravanMobileParty?.IsActive != true ||
            !parties.TryGet(IssueManager.GetIssueOfQuest(quest), out var party))
            return false;

        using (new MainHeroSubstitutionScope(Hero.MainHero, party))
        {
            BeHostileAction.ApplyEncounterHostileAction(party.Party, quest._questCaravanMobileParty.Party);
            if (quest.IsOngoing) quest.FailByPlayerHostileConsequences();
        }
        return true;
    }
}
