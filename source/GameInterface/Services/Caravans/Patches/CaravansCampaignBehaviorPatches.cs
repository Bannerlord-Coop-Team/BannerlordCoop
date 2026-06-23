using Autofac;
using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Caravans.Interfaces;
using GameInterface.Services.Caravans.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

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

    [HarmonyPatch(nameof(CaravansCampaignBehavior.CanTradeWith))]
    [HarmonyPrefix]
    public static bool CanTradeWithPrefix(ref CaravansCampaignBehavior __instance, ref bool __result, IFaction caravanFaction, IFaction targetFaction)
    {
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;
        var sessionCaravansPlayerDataInterface = container.Resolve<ISessionCaravansPlayerDataInterface>();

        // Handle check in sessionCaravansPlayerDataInterface to check for more than one player's caravan
        __result = sessionCaravansPlayerDataInterface.CanTradeWith(caravanFaction, targetFaction);

        return false;
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