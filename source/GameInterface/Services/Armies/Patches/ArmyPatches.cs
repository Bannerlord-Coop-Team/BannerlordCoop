using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Fiefs.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies.Patches;

/// <summary>
/// Patches for adding and remove party from and army
/// </summary>
[HarmonyPatch(typeof(Army))]
public class ArmyPatches
{

    [HarmonyPatch(typeof(Army), "OnAddPartyInternal")]
    [HarmonyPrefix]
    static bool OnAddPartyInternalPrefix(ref Army __instance, ref MobileParty mobileParty)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (PolicyProvider.AllowOriginalCalls) return true;

        if (ModInformation.IsClient) return false;

        var message = new MobilePartyInArmyAdded(mobileParty.StringId, __instance.LeaderParty.StringId);
        MessageBroker.Instance.Publish(mobileParty, message);

        return true;
    }

    [HarmonyPatch(typeof(Army), "OnRemovePartyInternal")]
    [HarmonyPrefix]
    static bool OnRemovePartyInternalPrefix(ref Army __instance, ref MobileParty mobileParty)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (PolicyProvider.AllowOriginalCalls) return true;

        if (ModInformation.IsClient) return false;

        var message = new MobilePartyInArmyRemoved(mobileParty.StringId, __instance.LeaderParty.StringId);
        
        MessageBroker.Instance.Publish(mobileParty, message);

        return true;
    }

    public static void AddMobilePartyInArmy(MobileParty mobileParty, Army army)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                ArmyExtensions.AddPartyInternal(mobileParty, army);
            }
        });
    }

    public static void RemoveMobilePartyInArmy(MobileParty mobileParty, Army army)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                ArmyExtensions.RemovePartyInternal(mobileParty, army);
            }
        });
    }
}