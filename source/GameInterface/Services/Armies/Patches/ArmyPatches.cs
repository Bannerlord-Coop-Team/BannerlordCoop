using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using SandBox.ViewModelCollection.Nameplate;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
namespace GameInterface.Services.Armies.Patches;

/// <summary>
/// Patches for adding and remove party from and army
/// </summary>
[HarmonyPatch(typeof(Army))]
public class ArmyPatches
{
    private static ILogger Logger = LogManager.GetLogger<Kingdom>();

    [HarmonyPatch(nameof(Army.OnAddPartyInternal))]
    [HarmonyPrefix]
    static bool OnAddPartyInternalPrefix(ref Army __instance, MobileParty mobileParty)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;

        var message = new MobilePartyInArmyAdded(__instance, mobileParty);
        MessageBroker.Instance.Publish(mobileParty, message);

        return true;
    }

    [HarmonyPatch(nameof(Army.OnRemovePartyInternal))]
    [HarmonyPrefix]
    static bool OnRemovePartyInternalPrefix(ref Army __instance, MobileParty mobileParty)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client removed managed {name} from {VariableName}", typeof(MobileParty), nameof(Army.OnRemovePartyInternal));
            return true;
        }

        var message = new MobilePartyInArmyRemoved(__instance, mobileParty);

        MessageBroker.Instance.Publish(mobileParty, message);

        return true;
    }
    [HarmonyPatch(typeof(Army), "set_ArmyOwner")]
    [HarmonyPostfix]
    static void ArmyOwnerSetPostfix(Army __instance)
    {
        if (ModInformation.IsClient)
        {
            __instance.UpdateName();
        }
    }
    
   
    public static void AddMobilePartyInArmy(MobileParty mobileParty, Army army)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                mobileParty.Army = army;
            }
        });
    }

    public static void RemoveMobilePartyInArmy(MobileParty mobileParty, Army army)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                army._parties.Remove(mobileParty);
                mobileParty.AttachedTo = null;
                mobileParty._army = null;
            }
        });
    }
}