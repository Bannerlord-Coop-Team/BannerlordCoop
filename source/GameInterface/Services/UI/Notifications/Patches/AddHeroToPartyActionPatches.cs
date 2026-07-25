using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.UI.Notifications.Patches;

[HarmonyPatch(typeof(AddHeroToPartyAction))]
internal class AddHeroToPartyActionPatches
{
    [HarmonyPatch(nameof(AddHeroToPartyAction.ApplyInternal))]
    [HarmonyPostfix]
    public static void ApplyInternalPostfix(Hero hero, MobileParty newParty, bool showNotification = true)
    {
        if (ModInformation.IsClient) return;

        // Don't notify players of hero joining non player parties
        if ((newParty == null || !newParty.IsPlayerParty())) return;

        var message = new NotifyHeroJoinedParty(newParty, hero);
        MessageBroker.Instance.Publish(null, message);
    }
}
