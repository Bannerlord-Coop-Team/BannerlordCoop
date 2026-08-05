using Common;
using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Kingdoms.Messages;
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
                MessageBroker.Instance.Publish(__instance, new StartRebellion(Clan.PlayerClan));
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
            MessageBroker.Instance.Publish(__instance, new MercenaryServiceDismissalAccepted(Clan.PlayerClan.Kingdom, Clan.PlayerClan));
        }
        else
        {
            MessageBroker.Instance.Publish(__instance, new VassalServiceLeft(Clan.PlayerClan));
        }
        __instance.ExecuteClose();
        return false;
    }

    /// <summary>
    /// Intercepts the client rename logic
    /// </summary>
    [HarmonyPatch("OnChangeKingdomName")]
    [HarmonyPrefix]
    private static bool OnChangeKingdomNamePrefix(KingdomManagementVM __instance, string __0)
    {
        if (!ModInformation.IsClient)
        {
            return true;
        }

        MessageBroker.Instance.Publish(__instance, new KingdomNameChangeRequested(__instance.Kingdom, __0));
        return false;
    }
}

    

