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
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Patches;

using Quest = VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest;

[HarmonyPatch(typeof(Quest), "OnWarDeclared")]
internal class VillageNeedsToolsQuestWarDeclaredGatePatch
{
    [HarmonyPrefix]
    internal static bool Prefix(Quest __instance, IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
    {
        if (ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var registry))
        {
            if (registry.IsLocalPeerOwner(__instance.QuestGiver))
            {
                Evaluate(__instance, faction1, faction2, detail);
            }
            else
            {
                DisconnectedOwnerEvaluationSupport.TryEvaluateOnBehalfOfDisconnectedOwner(
                    __instance.QuestGiver, controllerId => Evaluate(__instance, faction1, faction2, detail, controllerId));
            }

            return false;
        }

        return CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
    }

    private static void Evaluate(Quest quest, IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail, string controllerId = null)
    {
        if (!quest.QuestGiver.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)) return;

        if (DiplomacyHelper.IsWarCausedByPlayer(faction1, faction2, detail))
        {
            VillageNeedsToolsQuestType.PublishQuestFail(quest.QuestGiver, VillageNeedsToolsQuestType.ProofFailWar, controllerId);
        }
        else
        {
            VillageNeedsToolsQuestType.PublishTerminalOutcome(quest.QuestGiver, IssueFinalizeReason.QuestCancel, controllerId);
        }
    }
}

[HarmonyPatch(typeof(Quest), "OnClanChangedKingdom")]
internal class VillageNeedsToolsQuestClanChangedKingdomGatePatch
{
    [HarmonyPrefix]
    internal static bool Prefix(
        Quest __instance, Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
    {
        if (ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var registry))
        {
            if (registry.IsLocalPeerOwner(__instance.QuestGiver))
            {
                Evaluate(__instance);
            }
            else
            {
                DisconnectedOwnerEvaluationSupport.TryEvaluateOnBehalfOfDisconnectedOwner(
                    __instance.QuestGiver, controllerId => Evaluate(__instance, controllerId));
            }

            return false;
        }

        return CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
    }

    private static void Evaluate(Quest quest, string controllerId = null)
    {
        if (!quest.QuestGiver.CurrentSettlement.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)) return;

        VillageNeedsToolsQuestType.PublishTerminalOutcome(quest.QuestGiver, IssueFinalizeReason.QuestCancel, controllerId);
    }
}

[HarmonyPatch(typeof(Quest), "OnMapEventStarted")]
internal class VillageNeedsToolsQuestMapEventStartedGatePatch
{
    [HarmonyPrefix]
    internal static bool Prefix(Quest __instance, MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        if (ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var registry))
        {
            if (registry.IsLocalPeerOwner(__instance.QuestGiver))
            {
                Evaluate(__instance, mapEvent, attackerParty);
            }
            else
            {
                DisconnectedOwnerEvaluationSupport.TryEvaluateOnBehalfOfDisconnectedOwner(
                    __instance.QuestGiver, controllerId => Evaluate(__instance, mapEvent, attackerParty, controllerId));
            }

            return false;
        }

        return CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
    }

    private static void Evaluate(Quest quest, MapEvent mapEvent, PartyBase attackerParty, string controllerId = null)
    {
        if (!QuestHelper.CheckMinorMajorCoercion(quest, mapEvent, attackerParty)) return;

        VillageNeedsToolsQuestType.PublishQuestFail(quest.QuestGiver, VillageNeedsToolsQuestType.ProofFailCoercion, controllerId);
    }
}
