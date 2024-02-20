using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Extentions;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Handles Settlement AddMobileParty(MobileParty) and RemoveMobileParty(MobileParty) functions.
/// </summary>
[HarmonyPatch(typeof(Settlement))]
public class MobilePartyCachePatch
{

    [HarmonyPatch("AddMobileParty")]
    [HarmonyPrefix]
    private static bool AddMobilePartyPrefix(ref Settlement __instance, ref MobileParty mobileParty)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;

        var partiesCache = __instance.GetPartiesCache();

        if(!partiesCache.Contains(mobileParty))
        {
            partiesCache.Add(mobileParty);
            int lordParties = __instance.GetNumberOfLordPartiesAt();
            if (mobileParty.IsLordParty)
            {
                __instance.SetNumberOfLordPartiesAt(++lordParties);
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

        var partiesCache = __instance.GetPartiesCache();

        if (partiesCache.Contains(mobileParty))
        {
            partiesCache.Remove(mobileParty);
            int lordParties = __instance.GetNumberOfLordPartiesAt();
            if (mobileParty.IsLordParty)
            {
                __instance.SetNumberOfLordPartiesAt(--lordParties);
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
                    settlement.GetPartiesCache().Add(party);
                    settlement.SetNumberOfLordPartiesAt(numberOfLordParties);
                } else
                {
                    settlement.GetPartiesCache().Remove(party);
                    settlement.SetNumberOfLordPartiesAt(numberOfLordParties);
                }

            }
        });
    }
}
