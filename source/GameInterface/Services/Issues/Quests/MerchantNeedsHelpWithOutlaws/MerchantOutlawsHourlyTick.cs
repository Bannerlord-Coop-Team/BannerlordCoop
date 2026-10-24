using Common;
using GameInterface.Services.Issues.Framework.Interface;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssueQuest;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws;

internal interface IMerchantOutlawsHourlyTick
{
    void Tick(IEnumerable<MobileParty> parties);
}

/// <summary>
/// Shares the issue scan and owner scope across each hour's bandit evaluations.
/// </summary>
internal class MerchantOutlawsHourlyTick : IMerchantOutlawsHourlyTick
{
    private readonly IOutcomeDispatcher dispatcher;
    private readonly IMerchantOutlawsProgress progress;

    public MerchantOutlawsHourlyTick(IOutcomeDispatcher dispatcher, IMerchantOutlawsProgress progress)
    {
        this.dispatcher = dispatcher;
        this.progress = progress;
    }

    public void Tick(IEnumerable<MobileParty> parties)
    {
        if (ModInformation.IsClient) return;
        var bandits = parties.Where(party => party.IsBandit).ToArray();
        if (bandits.Length == 0) return;

        dispatcher.RunAsOwners<Quest>(quest =>
        {
            foreach (var party in bandits)
            {
                var wasValid = quest._validPartiesList.Contains(party);
                quest.HourlyTickParty(party);
                if (wasValid != quest._validPartiesList.Contains(party)) progress.Send(quest);
            }
        });
    }
}
