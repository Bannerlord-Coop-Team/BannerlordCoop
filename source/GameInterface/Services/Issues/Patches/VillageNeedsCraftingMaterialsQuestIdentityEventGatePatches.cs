using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.VillageNeedsCraftingMaterials;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Quest = VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest;

[HarmonyPatch(typeof(Quest), "OnWarDeclared")]
internal class VillageNeedsCraftingMaterialsQuestWarDeclaredGatePatch
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
            VillageNeedsCraftingMaterialsQuestType.PublishQuestFail(quest, VillageNeedsCraftingMaterialsQuestType.ProofFailWar);
        }
        else
        {
            VillageNeedsCraftingMaterialsQuestType.PublishTerminalOutcome(quest.QuestGiver, IssueFinalizeReason.QuestCancel);
        }
    }
}

[HarmonyPatch(typeof(Quest), "OnClanChangedKingdom")]
internal class VillageNeedsCraftingMaterialsQuestClanChangedKingdomGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        Quest __instance, Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var registry) && registry.IsLocalPeerOwner(__instance.QuestGiver))
        {
            Evaluate(__instance);
        }

        return false;
    }

    private static void Evaluate(Quest quest)
    {
        if (!quest.QuestGiver.CurrentSettlement.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)) return;

        VillageNeedsCraftingMaterialsQuestType.PublishTerminalOutcome(quest.QuestGiver, IssueFinalizeReason.QuestCancel);
    }
}
