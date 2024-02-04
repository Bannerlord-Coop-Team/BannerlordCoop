using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Common.Logging;
using Serilog;
namespace GameInterface.Services.Armies.Patches;

/// <summary>
/// Patches for adding and remove party from and army
/// </summary>
[HarmonyPatch(typeof(Army))]
public class ArmyPatches
{
    private static ILogger Logger = LogManager.GetLogger<Kingdom>();

    [HarmonyPatch(typeof(Army), "OnAddPartyInternal")]
    [HarmonyPrefix]
    static bool OnAddPartyInternalPrefix(ref Army __instance, ref MobileParty mobileParty)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (PolicyProvider.AllowOriginalCalls) return true;

        if (ModInformation.IsClient) return false;

        return true;
    }

    [HarmonyPatch(typeof(Army), "OnAddPartyInternal")]
    [HarmonyPostfix]
    static void OnAddPartyInternalPostfix(MobileParty mobileParty)
    {
        if (ModInformation.IsClient) return;

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            Logger.Error("Unable to resolve {objectManager}", typeof(IObjectManager));
            return;
        }
        string armyId = mobileParty.Army.GetStringId();
        if (armyId == null)
        {
            Logger.Error("{army} was not properly registered", mobileParty.Army.Name);
            return;
        }

        var message = new MobilePartyInArmyAdded(mobileParty.StringId, armyId);
        MessageBroker.Instance.Publish(mobileParty, message);

        return;
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