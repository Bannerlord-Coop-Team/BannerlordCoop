using Common;
using Common.Messaging;
using GameInterface.Services.UI.LogEntries.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.CommentBehaviors;

namespace GameInterface.Services.UI.LogEntries.Patches;

[HarmonyPatch(typeof(CommentOnClanLeaderChangedBehavior))]
internal class CommentOnClanLeaderChangedPatches
{
    [HarmonyPatch(nameof(CommentOnClanLeaderChangedBehavior.OnClanLeaderChanged))]
    [HarmonyPrefix]
    public static void OnClanLeaderChangedPrefix(Hero oldLeader, Hero newLeader)
    {
        if (ModInformation.IsClient) return;

        var message = new CommentClanLeaderChanged(oldLeader, newLeader);
        MessageBroker.Instance.Publish(null, message);
    }
}
