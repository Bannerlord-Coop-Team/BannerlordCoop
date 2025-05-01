using Common.Logging;
using Common.Messaging;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;

namespace GameInterface.Services.TroopRosters.Patches;

[HarmonyPatch(typeof(RecruitmentVM))]
public class OnDoneRecruitmentVMPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<OnDoneRecruitmentVMPatch>();

    [HarmonyPatch("OnDone")]
    [HarmonyPrefix]
    public static bool OnDonePrefix(ref RecruitmentVM __instance)
    {
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            Logger.Error("Unable to resolve {type}\n"
                    + "Callstack: {callstack}", typeof(IGameInterfaceConfig), Environment.StackTrace);
            return true;
        }

        if (config.IsServer) return true;

        string mobilePartyId = MobileParty.MainParty.StringId;

        List<(string, string, int)> troopsInCart = new();
        int totalCost = 0;
        
        foreach(RecruitVolunteerTroopVM recruitVolunteerTroopVM in __instance.TroopsInCart)
        {
            totalCost += Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(recruitVolunteerTroopVM.Character, Hero.MainHero, false);
            troopsInCart.Add((recruitVolunteerTroopVM.Owner.OwnerHero.StringId, recruitVolunteerTroopVM.Character.StringId, recruitVolunteerTroopVM.Index));
        }
        var message = new OnDoneRecruitmentVMChanged(mobilePartyId, troopsInCart.ToArray(), totalCost);

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
messageBroker?.Publish(__instance, message);

        return false;
    }
}