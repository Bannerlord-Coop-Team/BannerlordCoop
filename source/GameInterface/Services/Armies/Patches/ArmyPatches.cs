using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
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
        Logger.Debug($"OnAddPartyInternalPrefix");
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
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
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
        GameThread.Run(() =>
        {
            mobileParty.Army = army;
        });
    }

    public static void RemoveMobilePartyInArmy(MobileParty mobileParty, Army army)
    {
        GameThread.Run(() =>
        {
            mobileParty.Ai.SetInitiative(1f, 1f, 24f);
            army._parties.Remove(mobileParty);
            //CampaignEventDispatcher.Instance.OnPartyRemovedFromArmy(mobileParty); NRE
            if (army == MobileParty.MainParty.Army && !army._armyIsDispersing)
            {
                CampaignEventDispatcher.Instance.OnArmyOverlaySetDirty();
            }
            mobileParty.AttachedTo = null;
            if (army.LeaderParty == mobileParty && !army._armyIsDispersing)
            {
                DisbandArmyAction.ApplyByLeaderPartyRemoved(army);
            }
            if (mobileParty == MobileParty.MainParty)
            {
                Campaign.Current.CameraFollowParty = MobileParty.MainParty.Party;
                army.StopTrackingTargetSettlement();
            }
            if (((army != null) ? army.LeaderParty : null) == mobileParty)
            {
                army.FinishArmyObjective();
                if (!army._armyIsDispersing)
                {
                    Army army2 = mobileParty.Army;
                    if (((army2 != null) ? army2.LeaderParty.LeaderHero : null) == null)
                    {
                        DisbandArmyAction.ApplyByArmyLeaderIsDead(mobileParty.Army);
                    }
                    else
                    {
                        DisbandArmyAction.ApplyByObjectiveFinished(mobileParty.Army);
                    }
                }
            }
            else if (army.Parties.Count == 0 && !army._armyIsDispersing)
            {
                if (mobileParty.Army != null && MobileParty.MainParty.Army != null && mobileParty.Army == MobileParty.MainParty.Army && Hero.MainHero.IsPrisoner)
                {
                    DisbandArmyAction.ApplyByPlayerTakenPrisoner(army);
                }
                else
                {
                    DisbandArmyAction.ApplyByNotEnoughParty(army);
                }
            }
            mobileParty._army = null;
            if (mobileParty == MobileParty.MainParty && Game.Current.GameStateManager.ActiveState is MapState)
            {
                ((MapState)Game.Current.GameStateManager.ActiveState).OnLeaveArmy();
            }
            CampaignEventDispatcher.Instance.OnPartyLeftArmy(mobileParty, army);
            mobileParty.Party.SetVisualAsDirty();
            mobileParty.Party.UpdateVisibilityAndInspected(MobileParty.MainParty.Position, 0f);
            mobileParty.Ai.RethinkAtNextHourlyTick = true;
        });
    }
    public static void SetAiBehaviorObject(Army army, IMapPoint mapPoint)
    {
        GameThread.Run(() =>
        {
            using (new AllowedThread())
            {
                // Set field directly to avoid StopTrackingTargetSettlement/StartTrackingTargetSettlement
                // which are serverside ai behaviors not needed on client
                army._aiBehaviorObject = mapPoint;
            }
        });
    }
}