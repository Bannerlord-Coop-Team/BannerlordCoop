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
    static bool OnAddPartyInternalPrefix(ref Army __instance, MobileParty mobileParty)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;
        

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            Logger.Error("Unable to resolve {objectManager}", typeof(IObjectManager));
            return false;
        }

        string armyId = __instance.GetStringId();
        if (armyId == null)
        {
            Logger.Error("{army} was not properly registered", mobileParty.Army.Name);
            return false;
        }

        var message = new MobilePartyInArmyAdded(mobileParty.StringId, armyId);
        MessageBroker.Instance.Publish(mobileParty, message);

        return true;
    }


    [HarmonyPatch(typeof(Army), "OnRemovePartyInternal")]
    [HarmonyPrefix]
    static bool OnRemovePartyInternalPrefix(ref Army __instance, MobileParty mobileParty)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;
        

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            Logger.Error("Unable to resolve {objectManager}", typeof(IObjectManager));
            return false;
        }
        
        
        string armyId = __instance.GetStringId();
        if (armyId == null)
        {
            Logger.Error("{army} was not properly registered", mobileParty.Army.Name);
            return false;
        }

        var message = new MobilePartyInArmyRemoved(mobileParty.StringId, armyId);

        MessageBroker.Instance.Publish(mobileParty, message);
        

        return true;
    }


    public static void AddMobilePartyInArmy(MobileParty mobileParty, Army army)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                army.AddPartyInternal(mobileParty);

            }
        });
    }

    public static void RemoveMobilePartyInArmy(MobileParty mobileParty, Army army)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                army.RemovePartyInternal(mobileParty);
            }
        });
    }
}