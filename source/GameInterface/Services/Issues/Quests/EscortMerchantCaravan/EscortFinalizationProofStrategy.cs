using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using GameInterface.Services.Heroes.Patches;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Actions;
using Common.Logging;
using Serilog;
using Quest = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan;

internal enum EscortBranch : byte
{
    AttackCaravan,
}

internal sealed class EscortFinalizationProofStrategy : IFinalizationProofStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<EscortFinalizationProofStrategy>();
    private readonly IEscortOwnerParty parties;

    public EscortFinalizationProofStrategy(IEscortOwnerParty parties)
    {
        this.parties = parties;
    }

    public bool TryRunBranch(QuestBase questBase, byte proof)
    {
        if (!(questBase is Quest quest) || proof != (byte)EscortBranch.AttackCaravan) return false;
        if (!quest.IsOngoing || quest._questCaravanMobileParty?.IsActive != true ||
            !parties.TryGet(IssueManager.GetIssueOfQuest(quest), out var party))
        {
            Logger.Warning("Escort attack branch {proof} was already handled for {quest}", proof, quest.StringId);
            return true;
        }

        using (new MainHeroSubstitutionScope(Hero.MainHero, party))
        {
            BeHostileAction.ApplyEncounterHostileAction(party.Party, quest._questCaravanMobileParty.Party);
            if (quest.IsOngoing) quest.FailByPlayerHostileConsequences();
        }
        return true;
    }
}
