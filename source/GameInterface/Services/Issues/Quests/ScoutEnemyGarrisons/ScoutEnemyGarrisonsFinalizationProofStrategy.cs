using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons;

/// <summary>
/// Scouting progress only exists on the owning player's client, so the server takes its word that
/// every target was scouted and grants the renown, influence and relation as that player.
/// </summary>
internal class ScoutEnemyGarrisonsFinalizationProofStrategy : IFinalizationProofStrategy
{
    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest scoutQuest) || (ScoutEnemyGarrisonsBranch)proof != ScoutEnemyGarrisonsBranch.AllScoutingDone)
        {
            return false;
        }

        scoutQuest.AllScoutingDone();
        return true;
    }
}
