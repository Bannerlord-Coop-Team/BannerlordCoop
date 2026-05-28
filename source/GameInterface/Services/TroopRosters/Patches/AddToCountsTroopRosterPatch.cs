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
using Serilog;
using Common.Logging;

namespace GameInterface.Services.TroopRosters.Patches;


[HarmonyPatch(typeof(TroopRoster))]
public class AddToCountsTroopRosterPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<AddToCountsTroopRosterPatch>();

    [HarmonyPatch("AddToCounts")]
    [HarmonyPrefix]
    private static void AddToCountsPrefix(ref TroopRoster __instance, CharacterObject character, int count, bool insertAtFront,
        int woundedCount, int xpChange, bool removeDepleted, int index)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to update managed {type}, {methodName}", typeof(ItemRoster), nameof(ItemRoster.AddToCounts));
            return;
        }

        var message = new TroopRosterAddToCountsChanged(__instance, character, count, insertAtFront, woundedCount, xpChange, removeDepleted, index);

        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch("AddToCountsAtIndex")]
    [HarmonyPrefix]
    private static void AddToCountsAtIndexPrefix(ref TroopRoster __instance, int index, int countChange, int woundedCountChange, int xpChange, bool removeDepleted)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to update managed {type}, {methodName}", typeof(ItemRoster), "AddToCountsAtIndex");
            return;
        }

        var message = new TroopRosterAddToCountsAtIndexChanged(__instance, index, countChange, woundedCountChange, xpChange, removeDepleted);

        MessageBroker.Instance.Publish(__instance, message);
    }

    public static void RunAddToCounts(TroopRoster troopRoster, CharacterObject character, int count, bool insertAtFront, int woundedCount, int xpChange, bool removeDepleted, int index)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                troopRoster.AddToCounts(character, count, insertAtFront, woundedCount, xpChange, removeDepleted, index);
            }
        });
    }

    public static void RunAddToCountsAtIndex(TroopRoster troopRoster, int index, int count, int woundedCount, int xpChange, bool removeDepleted)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                troopRoster.AddToCountsAtIndex(index, count, woundedCount, xpChange, removeDepleted);
            }
        });
    }
}