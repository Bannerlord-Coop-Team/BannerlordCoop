using Common.Messaging;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;

namespace GameInterface.Services.TroopRosters.Patches;

[HarmonyPatch(typeof(RecruitmentVM))]
public class OnDoneRecruitmentVMPatch
{
    [HarmonyPatch("OnDone")]
    [HarmonyPrefix]
    public static bool OnDonePrefix(ref RecruitmentVM __instance)
    {

        if (ModInformation.IsServer) return true;

        string mobilePartyId = MobileParty.MainParty.StringId;

        List<(string, string, int)> troopsInCart = new();

        
        foreach(RecruitVolunteerTroopVM recruitVolunteerTroopVM in __instance.TroopsInCart)
        {
            troopsInCart.Add((recruitVolunteerTroopVM.Owner.OwnerHero.StringId, recruitVolunteerTroopVM.Character.StringId, recruitVolunteerTroopVM.Index));
        }
        var message = new OnDoneRecruitmentVMChanged(mobilePartyId, troopsInCart.ToArray());

        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
}
