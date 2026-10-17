using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsWeapons;

internal class GangLeaderNeedsWeaponsFinalizationProofStrategy : IFinalizationProofStrategy
{
    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest weaponsQuest))
        {
            return false;
        }

        switch ((GangLeaderNeedsWeaponsBranch)proof)
        {
            case GangLeaderNeedsWeaponsBranch.DeliverWeapons:
                weaponsQuest.PlayerSuccessfullyDeliveredWeapons();
                return true;
            default:
                return false;
        }
    }
}
