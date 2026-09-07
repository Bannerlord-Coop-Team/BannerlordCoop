using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.VillageNeedsTools;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Quest = VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest;

[HarmonyPatch(typeof(Quest), "OnWarDeclared")]
internal class VillageNeedsToolsQuestWarDeclaredGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(Quest __instance, IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var registry) && registry.IsLocalPeerOwner(__instance.QuestGiver))
        {
            Evaluate(__instance, faction1, faction2, detail);
        }

        return false;
    }

    private static void Evaluate(Quest quest, IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
    {
        if (!quest.QuestGiver.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)) return;

        if (DiplomacyHelper.IsWarCausedByPlayer(faction1, faction2, detail))
        {
            VillageNeedsToolsQuestType.PublishQuestFail(quest.QuestGiver, VillageNeedsToolsQuestType.ProofFailWar);
        }
        else
        {
            VillageNeedsToolsQuestType.PublishTerminalOutcome(quest.QuestGiver, IssueFinalizeReason.QuestCancel);
        }
    }
}
