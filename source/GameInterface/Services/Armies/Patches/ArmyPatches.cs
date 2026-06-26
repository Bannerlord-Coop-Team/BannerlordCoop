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
            return false;
        }

        var message = new MobilePartyInArmyRemoved(__instance, mobileParty, null); // mobileparty.mainparty is only needed for the client for ui stuff

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
        GameThread.RunSafe(() =>
        {
            army._parties.Add(mobileParty);
            mobileParty._army = army;
            mobileParty.Ai.RethinkAtNextHourlyTick = true;
            CampaignEventDispatcher.Instance.OnPartyJoinedArmy(mobileParty);
        });
    }

    public static void RemoveMobilePartyInArmy(MobileParty mobileParty, Army army, MobileParty clientMobileParty)
    {
        GameThread.RunSafe(() =>
        {
            mobileParty.Ai.SetInitiative(1f, 1f, 24f);
            army._parties.Remove(mobileParty);
            if (army == clientMobileParty?.Army && !army._armyIsDispersing)
            {
                CampaignEventDispatcher.Instance.OnArmyOverlaySetDirty();
            }
            mobileParty.AttachedTo = null;
            if (army.LeaderParty == mobileParty && !army._armyIsDispersing)
            {
                DisbandArmyAction.ApplyByLeaderPartyRemoved(army);
            }
            if (mobileParty == clientMobileParty)
            {
                Campaign.Current.CameraFollowParty = clientMobileParty?.Party;
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
                if (mobileParty.Army != null && clientMobileParty?.Army != null && mobileParty.Army == clientMobileParty?.Army && (clientMobileParty?.LeaderHero?.IsPrisoner ?? false))
                {
                    DisbandArmyAction.ApplyByPlayerTakenPrisoner(army);
                }
                else
                {
                    DisbandArmyAction.ApplyByNotEnoughParty(army);
                }
            }
            // Mainplayer cant have an army with only itself
            if (army.LeaderParty == clientMobileParty && army.Parties.Count <= 1)
            {
                DisbandArmyAction.ApplyByNotEnoughParty(army);
            }
            if (mobileParty == clientMobileParty && Game.Current.GameStateManager.ActiveState is MapState)
            {
                ((MapState)Game.Current.GameStateManager.ActiveState).OnLeaveArmy();
            }
            CampaignEventDispatcher.Instance.OnPartyLeftArmy(mobileParty, army);
            mobileParty.Party.SetVisualAsDirty();
            if (clientMobileParty != null)
            {
                mobileParty.Party.UpdateVisibilityAndInspected(clientMobileParty.Position, 0f);
            }
            if (clientMobileParty != mobileParty)
            {
                mobileParty.Ai.RethinkAtNextHourlyTick = true;
            }
        });
    }
    public static void SetAiBehaviorObject(Army army, IMapPoint mapPoint)
    {
        GameThread.RunSafe(() =>
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