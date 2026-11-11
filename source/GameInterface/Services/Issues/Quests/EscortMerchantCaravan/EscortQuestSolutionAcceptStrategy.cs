using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan;

internal sealed class EscortQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private readonly IIssueOwnerResolver owners;
    private readonly IEscortProgress progress;
    private readonly IEscortOwnerParty parties;

    public EscortQuestSolutionAcceptStrategy(IIssueOwnerResolver owners, IEscortProgress progress, IEscortOwnerParty parties)
    {
        this.owners = owners;
        this.progress = progress;
        this.parties = parties;
    }

    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (ModInformation.IsClient || !(issue.IssueQuest is Quest quest)) return;
        if (!owners.TryResolveOwnerHero(issue, out var owner) || !parties.TryGet(issue, out var party))
            throw new InvalidOperationException("Escort accept has no owning player.");

        using (new MainHeroSubstitutionScope(owner, party))
        {
            quest._playerStartsQuestLog = quest.AddDiscreteLog(quest.PlayerStartsQuestLogText,
                new TextObject("{=r2y3n7dR}Visited Settlements"), 0, quest._requiredSettlementNumber);
            try
            {
                quest.SpawnCaravan();
                progress.Publish(quest);
            }
            catch
            {
                if (quest._questCaravanMobileParty?.IsActive == true)
                    DestroyPartyAction.Apply(null, quest._questCaravanMobileParty);
                quest._questCaravanMobileParty = null;
                throw;
            }
        }
    }
}
