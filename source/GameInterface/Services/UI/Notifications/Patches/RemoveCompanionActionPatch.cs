using Common.Messaging;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.UI.Notifications.Patches;

/// <summary>
/// Patch remove companions from action rather than event to use clan
/// clan is used to only show the notification for player(s) of this clan
/// </summary>
[HarmonyPatch(typeof(RemoveCompanionAction))]
internal class RemoveCompanionActionPatch
{
    [HarmonyPatch(nameof(RemoveCompanionAction.ApplyInternal))]
    [HarmonyPostfix]
    public static void ApplyInternalPostfix(Clan clan, Hero companion, RemoveCompanionAction.RemoveCompanionDetail detail)
    {
        var message = new NotifyCompanionRemoved(clan, companion, detail);
        MessageBroker.Instance.Publish(null, message);
    }
}
