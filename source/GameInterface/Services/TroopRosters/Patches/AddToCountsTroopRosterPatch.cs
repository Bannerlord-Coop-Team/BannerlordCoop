using HarmonyLib;
using Common.Messaging;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Roster;
using GameInterface.Services.TroopRosters.Messages;
using GameInterface.Policies;
using Common;
using Common.Util;

namespace GameInterface.Services.TroopRosters.Patches;


[HarmonyPatch(typeof(TroopRoster))]
public class AddToCountsTroopRosterPatch
{
    [HarmonyPatch("AddToCounts")]
    [HarmonyPrefix]
    private static bool AddToCountsPrefix(ref TroopRoster __instance, CharacterObject character, int count, bool insertAtFront,
        int woundedCount, int xpChange, bool removeDepleted, int index)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient) return false;
        // Owner Party
        // TODO: use publicizer later when it comes out
        if(__instance.OwnerParty == null) return false;

        MobileParty mobileParty = __instance.OwnerParty.MobileParty;

        var message = new TroopRosterAddToCountsChanged(mobileParty, character, count, insertAtFront, woundedCount, xpChange, removeDepleted, index);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch("AddToCountsAtIndex")]
    [HarmonyPrefix]
    private static bool AddToCountsAtIndexPrefix(ref TroopRoster __instance, int index, int countChange, int woundedCountChange, int xpChange, bool removeDepleted)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;

        MobileParty mobileParty = __instance.OwnerParty.MobileParty;

        var message = new TroopRosterAddToCountsAtIndexChanged(mobileParty.StringId, index, countChange, woundedCountChange, xpChange, removeDepleted);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    public static void RunAddToCounts(MobileParty party, CharacterObject character, int count, bool insertAtFront, int woundedCount, int xpChange, bool removeDepleted, int index)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                if(party.Party == null || party.MemberRoster == null) return;
                party.MemberRoster.AddToCounts(character, count, insertAtFront, woundedCount, xpChange, removeDepleted, index);
            }
        });
    }

    public static void RunAddToCountsAtIndex(MobileParty party, int index, int count, int woundedCount, int xpChange, bool removeDepleted)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                if (party.Party == null || party.MemberRoster == null) return;
                party.MemberRoster.AddToCountsAtIndex(index, count, woundedCount, xpChange, removeDepleted);
            }
        });
    }
}