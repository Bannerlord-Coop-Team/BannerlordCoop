using Common;
using Common.Messaging;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using System.Collections.Generic;
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

        List<(string, string, int)> troopsInCart = new();
        int totalCost = 0;
        
        foreach(RecruitVolunteerTroopVM recruitVolunteerTroopVM in __instance.TroopsInCart)
        {
            totalCost += Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(recruitVolunteerTroopVM.Character, Hero.MainHero).RoundedResultNumber;
            troopsInCart.Add((recruitVolunteerTroopVM.Owner.OwnerHero.StringId, recruitVolunteerTroopVM.Character.StringId, recruitVolunteerTroopVM.Index));
        }

        var message = new OnDoneRecruitmentVMChanged(mobilePartyId, troopsInCart.ToArray(), totalCost);

        MessageBroker.Instance.Publish(__instance, message);

        __instance.Deactivate();

        return false;
    }
}