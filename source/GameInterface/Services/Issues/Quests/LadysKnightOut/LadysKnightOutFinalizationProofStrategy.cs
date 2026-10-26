using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Common.Logging;
using Serilog;
using Quest = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut;

internal class LadysKnightOutFinalizationProofStrategy : IFinalizationProofStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<LadysKnightOutFinalizationProofStrategy>();
    private readonly ILadysKnightOutTournamentProgress progress;

    public LadysKnightOutFinalizationProofStrategy(ILadysKnightOutTournamentProgress progress)
    {
        this.progress = progress;
    }

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest ladyQuest) || quest.QuestGiver.Issue == null ||
            !progress.TryTakeOutcome(quest.QuestGiver.Issue.StringId, (LadysKnightOutBranch)proof))
        {
            Logger.Warning("No tournament outcome for quest {quest} and branch {proof}", quest.StringId, proof);
            return false;
        }

        if ((LadysKnightOutBranch)proof == LadysKnightOutBranch.TournamentSuccess)
        {
            ladyQuest.PlayerReachedTournamentRoundGoal();
        }
        else
        {
            ladyQuest.PlayerCouldntReachedTournamentRoundGoal();
        }
        return true;
    }
}
