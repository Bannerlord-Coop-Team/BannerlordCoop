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

    private static readonly FieldInfo TroopRoster_OwnerParty = typeof(Settlement).GetField("_partiesCache", BindingFlags.NonPublic | BindingFlags.Instance);


    [HarmonyPatch("AddToCounts")]
    [HarmonyPrefix]
    private static bool AddToCountsPrefix(ref TroopRoster __instance, CharacterObject character, int count, bool insertAtFront,
        int woundedCount, int xpChange, bool removeDepleted, int index)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient) return false;
        // Owner Party
        // TODO: use publicizer later when it comes out
        PartyBase partyBase = (PartyBase)TroopRoster_OwnerParty.GetValue(__instance);
        MobileParty mobileParty = partyBase.MobileParty;


        var message = new TroopRosterAddToCountsChanged(mobileParty.StringId, character.StringId, count, insertAtFront, woundedCount, xpChange, removeDepleted, index);

        MessageBroker.Instance.Publish(__instance, message);


        return true;
    }

    public static void RunAddToCounts(MobileParty party, CharacterObject character, int count, bool insertAtFront, int woundedCount, int xpChange, bool removeDepleted, int index)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(character, count, insertAtFront, woundedCount, xpChange, removeDepleted, index);
            }
        });
    }
}
