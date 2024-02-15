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
using System.Collections.Generic;
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

        // Get a list of the string id of the parties in the army
        List<string> partyStringIds = new List<string>();
        foreach (MobileParty party in __instance.Parties)
        {
            partyStringIds.Add(party.StringId);
        }
        //if the list does not contain the party, add it
        if (!partyStringIds.Contains(mobileParty.StringId))
        {
            partyStringIds.Add(mobileParty.StringId);
        }

        var message = new MobilePartyInArmyAdded(partyStringIds, armyId);
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
        //Get a list of the string id of the parties in the army
        
        List<string> partyStringIds = new List<string>();
        foreach (MobileParty party in __instance.Parties)
        {
            partyStringIds.Add(party.StringId);
        }

        if (partyStringIds.Contains(mobileParty.StringId))
        {
            partyStringIds.Remove(mobileParty.StringId);
        }

        var message = new MobilePartyInArmyRemoved(partyStringIds, armyId);

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

    public static void SetMobilePartyListInArmy(List<MobileParty> mobilePartyList, Army army)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                army.SetPartyList(mobilePartyList);
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