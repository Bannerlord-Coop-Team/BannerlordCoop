using Common;
using Common.Messaging;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;

namespace GameInterface.Services.TroopRosters.Patches;

[HarmonyPatch(typeof(RecruitmentVM))]
public class RecruitmentVMPatches
{
    [HarmonyPatch("OnDone")]
    [HarmonyPrefix]
    public static bool OnDonePrefix(ref RecruitmentVM __instance)
    {
        if (ModInformation.IsServer) return true;

        string mobilePartyId = MobileParty.MainParty.StringId;

        var troopsInCart = __instance.TroopsInCart.Select(
            recruitVolunteerTroopVM => new TroopInfo(
                recruitVolunteerTroopVM.Owner.OwnerHero.StringId,
                recruitVolunteerTroopVM.Character.StringId,
                recruitVolunteerTroopVM.Index
            ));

        var message = new RecruitmentAttempted(mobilePartyId, troopsInCart.ToArray());

        MessageBroker.Instance.Publish(__instance, message);

        __instance.Deactivate();

        return false;
    }
}