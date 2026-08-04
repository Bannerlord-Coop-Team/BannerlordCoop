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
    public static void ApplyInternalPostfix(Clan clan, Hero companion, RemoveCompanionAction.RemoveCompanionDetail detail, bool __runOriginal)
    {
        // RemoveCompanionActionPatches' prefix blocks ApplyInternal from running on a non-host
        // client (it forwards the attempt to the server instead) and also short-circuits stale/
        // non-member removals. Harmony still runs this postfix in both of those cases even though
        // nothing actually happened, so gate on __runOriginal - it only announces a removal that
        // really applied, mirroring MapEventPatches.Postfix_FinalizeEventAux's __runOriginal guard.
        if (!__runOriginal) return;

        var message = new NotifyCompanionRemoved(clan, companion, detail);
        MessageBroker.Instance.Publish(null, message);
    }
}
