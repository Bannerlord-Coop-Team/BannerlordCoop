using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Patches;


[HarmonyPatch(typeof(TroopRoster))]
public class AddToCountsTroopRosterPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<AddToCountsTroopRosterPatch>();

    [HarmonyPatch("AddToCounts")]
    [HarmonyPrefix]
    private static bool AddToCountsPrefix(ref TroopRoster __instance, CharacterObject character, int count, bool insertAtFront,
        int woundedCount, int xpChange, bool removeDepleted, int index)
    {
        if (CallPolicy.IsOriginalAllowed()) return true;
        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;
        if (__instance.OwnerParty == null) return false;

        MobileParty mobileParty = __instance.OwnerParty.MobileParty;


        var message = new TroopRosterAddToCountsChanged(mobileParty.StringId, character.StringId, count, insertAtFront, woundedCount, xpChange, removeDepleted, index);

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(__instance, message);


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
}
