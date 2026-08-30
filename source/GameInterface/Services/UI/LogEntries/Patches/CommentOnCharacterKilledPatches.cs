using Common;
using Common.Messaging;
using GameInterface.Services.UI.LogEntries.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors.CommentBehaviors;

namespace GameInterface.Services.UI.LogEntries.Patches;

[HarmonyPatch(typeof(CommentOnCharacterKilledBehavior))]
internal class CommentOnCharacterKilledPatches
{
    [HarmonyPatch(nameof(CommentOnCharacterKilledBehavior.OnBeforeHeroKilled))]
    [HarmonyPrefix]
    public static bool OnBeforeHeroKilledPrefix(CommentOnCharacterKilledBehavior __instance, Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail)
    {
        if (ModInformation.IsClient) return true;

        var message = new CommentHeroKilled(victim, killer, detail);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
}
