using Common;
using Common.Messaging;
using GameInterface.Services.UI.LogEntries.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.CommentBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.UI.LogEntries.Patches;

[HarmonyPatch(typeof(CommentOnChangeVillageStateBehavior))]
internal class CommentOnChangeVillageStatePatches
{
    [HarmonyPatch(nameof(CommentOnChangeVillageStateBehavior.OnVillageStateChanged))]
    [HarmonyPrefix]
    public static void OnVillageStateChangedPrefix(CommentOnChangeVillageStateBehavior __instance, Village village, Village.VillageStates oldState, Village.VillageStates newState, MobileParty raiderParty)
    {
        if (ModInformation.IsClient) return;

        // vanilla only logs a change with a raider, and a village going back to normal never has one
        if (raiderParty == null) return;

        var message = new CommentVillageStateChanged(village, oldState, newState, raiderParty);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
