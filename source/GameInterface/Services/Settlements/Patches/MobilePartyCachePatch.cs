using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Handles Settlement AddMobileParty(MobileParty) and RemoveMobileParty(MobileParty) functions.
/// </summary>
[HarmonyPatch(typeof(Settlement))]
public class MobilePartyCachePatch
{
    private static ILogger Logger = LogManager.GetLogger<Settlement>();


    [HarmonyPatch("AddMobileParty")]
    [HarmonyPrefix]
    private static bool AddMobilePartyPrefix(ref Settlement __instance, ref MobileParty mobileParty)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Settlement), Environment.StackTrace);
            return true;
        }

        var partiesCache = __instance._partiesCache;

        if(!partiesCache.Contains(mobileParty))
        {
            partiesCache.Add(mobileParty);
            int lordParties = __instance._numberOfLordPartiesAt;
            if (mobileParty.IsLordParty)
            {
                __instance._numberOfLordPartiesAt = ++lordParties;
            }
            // SettlementId
            // MobilePartyId
            // LordParties Value
            // bool add or remove
            var message = new SettlementChangedMobileParty(__instance.StringId, mobileParty.StringId, lordParties, true);
            MessageBroker.Instance.Publish(__instance, message);
        }

        return false;
    }

    [HarmonyPatch("RemoveMobileParty")]
    [HarmonyPrefix]
    private static bool RemoveMobilePartyPrefix(ref Settlement __instance, ref MobileParty mobileParty)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;

        var partiesCache = __instance._partiesCache;

        if (partiesCache.Contains(mobileParty))
        {
            partiesCache.Remove(mobileParty);
            int lordParties = __instance._numberOfLordPartiesAt;
            if (mobileParty.IsLordParty)
            {
                __instance._numberOfLordPartiesAt = --lordParties;
            }
            // SettlementId
            // MobilePartyId
            // LordParties Value
            // bool add or remove
            var message = new SettlementChangedMobileParty(__instance.StringId, mobileParty.StringId, lordParties, false);
            MessageBroker.Instance.Publish(__instance, message);

        }

        return false;
    }

    internal static void RunMobileParty(Settlement settlement, MobileParty party, int numberOfLordParties, bool AddMobileParty)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                if (AddMobileParty)
                {
                    settlement._partiesCache.Add(party);
                    settlement._numberOfLordPartiesAt = numberOfLordParties;
                } else
                {
                    settlement._partiesCache.Remove(party);
                    settlement.SettlementHitPoints = numberOfLordParties;
                }

            }
        });
    }
}
