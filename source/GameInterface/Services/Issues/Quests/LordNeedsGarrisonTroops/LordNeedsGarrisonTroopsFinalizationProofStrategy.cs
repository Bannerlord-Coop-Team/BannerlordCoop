using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordNeedsGarrisonTroops;

internal class LordNeedsGarrisonTroopsFinalizationProofStrategy : IFinalizationProofStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<LordNeedsGarrisonTroopsFinalizationProofStrategy>();

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest garrisonQuest) || (LordNeedsGarrisonTroopsBranch)proof != LordNeedsGarrisonTroopsBranch.TroopsDelivered)
        {
            return false;
        }

        if (!CanDeliver(garrisonQuest))
        {
            Logger.Warning("The owner of {quest} no longer carries the requested garrison troops", garrisonQuest.StringId);
            return true;
        }

        garrisonQuest.PlayerTransferredTroopsToGarrisonCommander();
        return true;
    }

    private static bool CanDeliver(Quest quest)
    {
        var party = MobileParty.MainParty;
        return quest.IsOngoing && party != null &&
            party.MemberRoster.GetTroopCount(quest._requestedTroopType) >= quest._requestedTroopAmount;
    }
}
