using Autofac;
using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Caravans.Interfaces;
using GameInterface.Services.Caravans.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Caravans.Patches;

[HarmonyPatch(typeof(CaravansCampaignBehavior))]
internal class DisableCaravansCampaignBehaviorPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.OnSettlementEntered)),
        AccessTools.Method(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.OnSettlementLeft)),
        AccessTools.Method(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.DailyTick)),
        AccessTools.Method(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.DailyTickHero)),
        AccessTools.Method(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.HourlyTickParty)),
        //AccessTools.Method(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.OnSessionLaunched)), // Needed on client to load dialogue for interacting with caravans
        AccessTools.Method(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.OnNewGameCreatedPartialFollowUpEndEvent)),
        AccessTools.Method(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.OnMobilePartyDestroyed)),
        AccessTools.Method(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.OnMobilePartyCreated)),
        AccessTools.Method(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.OnMapEventEnded)),
        AccessTools.Method(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.OnLootDistributedToParty)),
        AccessTools.Method(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.OnSiegeEventStarted)),
        AccessTools.Method(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.OnGameLoadFinished)),
        AccessTools.Method(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.OnKingdomDestroyed))
    };

    static bool Prefix()
    {
        return ModInformation.IsServer;
    }
}

[HarmonyPatch(typeof(CaravansCampaignBehavior))]
internal class CaravansCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(CaravansCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    public static bool RegisterEventsPrefix(ref CaravansCampaignBehavior __instance)
    {
        if (ModInformation.IsServer) return true;

        // Needs to run on the client to initialize carvan dialogue options
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(__instance, new Action<CampaignGameStarter>(__instance.OnSessionLaunched));
        return false;
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.OnKingdomDestroyed))]
    [HarmonyPostfix]
    public static void OnKingdomDestroyedPostfix(ref CaravansCampaignBehavior __instance, Kingdom destroyedKingdom)
    {
        // Update CoopSession.CaravansPlayerData by removing the kingdom if any player has it prohibited for trading
        var message = new CaravansKingdomDestroyed(destroyedKingdom);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.OnMobilePartyDestroyed))]
    [HarmonyPostfix]
    public static void OnMobilePartyDestroyedPostfix(ref CaravansCampaignBehavior __instance, MobileParty mobileParty, PartyBase destroyerParty)
    {
        // Don't publish message if it wasn't a caravan that was destroyed
        if (!mobileParty.IsCaravan) return;

        // Update CoopSession.CaravansPlayerData by removing the interacted caravan from all players' interaction history
        var message = new CaravanPartyDestroyed(mobileParty);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.DeleteExpiredTradeRumorTakenCaravans))]
    [HarmonyPrefix]
    public static bool DeleteExpiredTradeRumorTakenCaravansPrefix(ref CaravansCampaignBehavior __instance)
    {
        // Override implementation to use CoopSession on the server and update client instances of _tradeRumorTakenCaravans
        var message = new DeleteExpiredTradeRumorTakenCaravans();
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.DeleteExpiredLootedCaravans))]
    [HarmonyPrefix]
    public static bool DeleteExpiredLootedCaravansPrefix(ref CaravansCampaignBehavior __instance)
    {
        var message = new DeleteExpiredTradeRumorTakenCaravans();
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.CanTradeWith))]
    [HarmonyPrefix]
    public static bool CanTradeWithPrefix(ref CaravansCampaignBehavior __instance, ref bool __result, IFaction caravanFaction, IFaction targetFaction)
    {
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;
        var sessionCaravansPlayerDataInterface = container.Resolve<ISessionCaravansPlayerDataInterface>();

        // Handle check in sessionCaravansPlayerDataInterface to correctly handle prohibiting caravan trading with player blocked kingdoms
        __result = sessionCaravansPlayerDataInterface.CanTradeWith(caravanFaction, targetFaction, CaravansContext.CurrentParty);

        return false;
    }

    /// <summary>
    /// The following patches are needed to send the mobileParty for the patched implementation of CanTradeWith.
    /// The vanilla implementation is problematic because it only has factions as arguments when we need
    /// to be able to know who caravans belong to in accounting for prohibiting trading with kingdoms correctly.
    /// </summary>
    [HarmonyPatch(nameof(CaravansCampaignBehavior.HourlyTickParty))]
    [HarmonyPrefix]
    public static void HourlyTickPartyPrefix(MobileParty mobileParty)
    {
        CaravansContext.CurrentParty = mobileParty;
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.HourlyTickParty))]
    [HarmonyPostfix]
    public static void HourlyTickPartyPostfix()
    {
        CaravansContext.CurrentParty = null;
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.FindNextDestinationForCaravan))]
    [HarmonyPrefix]
    public static void FindNextDestinationForCaravanPrefix(MobileParty caravanParty)
    {
        CaravansContext.CurrentParty = caravanParty;
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.FindNextDestinationForCaravan))]
    [HarmonyPostfix]
    public static void FindNextDestinationForCaravanPostfix()
    {
        CaravansContext.CurrentParty = null;
    }

    // Potentially replace with transpiler later
    [HarmonyPatch(nameof(CaravansCampaignBehavior.OnSettlementLeft))]
    [HarmonyPrefix]
    public static void OnSettlementLeftPrefix(ref CaravansCampaignBehavior __instance, MobileParty mobileParty, Settlement settlement)
    {
        // Replace Static Mobile.MainParty check with IsPlayerParty()
        if (mobileParty != null && !mobileParty.IsPlayerParty() && (mobileParty.IsCaravan || mobileParty.IsLordParty))
        {
            int inventoryCapacity = mobileParty.InventoryCapacity;
            float totalWeightCarried = mobileParty.TotalWeightCarried;
            Town town = settlement.IsTown ? settlement.Town : (settlement.IsVillage ? settlement.Village.Bound.Town : null);
            if (town != null)
            {
                float num = 1.1f;
                while (totalWeightCarried > (float)inventoryCapacity)
                {
                    __instance.SellGoods(mobileParty, town, num, true);
                    num -= 0.02f;
                    if (num < 0.75f)
                    {
                        break;
                    }
                    inventoryCapacity = mobileParty.InventoryCapacity;
                    totalWeightCarried = mobileParty.TotalWeightCarried;
                }
            }
        }

        // Send message to clients to update TradeActionLogs for this MobileParty
        var message = new UpdateTradeActionLogsForParty(mobileParty, __instance._tradeActionLogs[mobileParty]);
        MessageBroker.Instance.Publish(__instance, message);
    }
}

[HarmonyPatch]
internal class CaravansAllowedThreadPatches
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.BribeAmount)) // Creates and modifies item rosters on clients
    };

    static void Prefix()
    {
        AllowedThread.AllowThisThread();
    }

    static void Finalizer()
    {
        AllowedThread.RevokeThisThread();
    }
}

/// <summary>
/// Used by CanTradeWithPrefix to include a mobileParty as an extra parameter in custom implementation
/// provided by SessionCaravansPlayerDataInterface.
/// </summary>
public static class CaravansContext
{
    [ThreadStatic]
    public static MobileParty CurrentParty;
}