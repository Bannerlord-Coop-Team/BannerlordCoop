using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement;
using TaleWorlds.Core;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(KingdomManagementVM))]
internal class KingdomManagementVMPatches
{
    [HarmonyPatch(nameof(KingdomManagementVM.OnConfirmLeaveKingdomWithOption))]
    [HarmonyPrefix]
    private static bool OnConfirmLeaveKingdomWithOptionPrefix(KingdomManagementVM __instance, List<InquiryElement> obj)
    {
        InquiryElement inquiryElement = obj.FirstOrDefault<InquiryElement>();
        if (inquiryElement != null)
        {
            string a = inquiryElement.Identifier as string;
            if (a == "keep")
            {
                //TODO
                //ChangeKingdomAction.ApplyByLeaveWithRebellionAgainstKingdom(Clan.PlayerClan, true);
            }
            else if (a == "dontkeep")
            {
                MessageBroker.Instance.Publish(__instance, new VassalServiceLeft(Clan.PlayerClan));
            }
            __instance.ExecuteClose();
        }
        return false;
    }
    [HarmonyPatch(nameof(KingdomManagementVM.OnConfirmLeaveKingdom))]
    [HarmonyPrefix]
    private static bool OnConfirmLeaveKingdomPrefix(KingdomManagementVM __instance)
    {
        if (Clan.PlayerClan.IsUnderMercenaryService)
        {
            MessageBroker.Instance.Publish(__instance, new MercenaryServiceDismissalAccepted(Clan.PlayerClan.Kingdom));
        }
        else
        {
            MessageBroker.Instance.Publish(__instance, new VassalServiceLeft(Clan.PlayerClan));
        }
        __instance.ExecuteClose();
        return false;
    }
}
