using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
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

    [HarmonyPatch(nameof(Army.OnAddPartyInternal))]
    [HarmonyPrefix]
    static bool OnAddPartyInternalDebugPrefix(ref Army __instance, MobileParty mobileParty)
    {
        __instance._parties.Add(mobileParty);
        mobileParty.Ai.RethinkAtNextHourlyTick = true;
        CampaignEventDispatcher.Instance.OnPartyJoinedArmy(mobileParty);
        if (__instance == MobileParty.MainParty.Army && __instance.LeaderParty != MobileParty.MainParty)
        {
            __instance.StartTrackingTargetSettlement(__instance.AiBehaviorObject);
            CampaignEventDispatcher.Instance.OnArmyOverlaySetDirty();
        }
        if (!mobileParty.IsMainParty)
        {
            mobileParty.Ai.RethinkAtNextHourlyTick = true;
        }
        if (mobileParty != MobileParty.MainParty && __instance.LeaderParty != MobileParty.MainParty && __instance.LeaderParty.LeaderHero != null)
        {
            int num = -Campaign.Current.Models.ArmyManagementCalculationModel.CalculatePartyInfluenceCost(__instance.LeaderParty, mobileParty);
            ChangeClanInfluenceAction.Apply(__instance.LeaderParty.LeaderHero.Clan, (float)num);
        }

        return false;
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


    public static void AddMobilePartyInArmy(MobileParty mobileParty, Army army)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                // TODO find why this is not getting set automatically
                if (mobileParty.Army == null)
                {
                    mobileParty._army = army;
                }
                army.OnAddPartyInternal(mobileParty);
            }
        });
    }

    public static void RemoveMobilePartyInArmy(MobileParty mobileParty, Army army)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                // TODO find why this is not getting set automatically
                if (mobileParty.Army == null)
                {
                    mobileParty._army = army;
                }

                army.OnRemovePartyInternal(mobileParty);
            }
        });
    }
}