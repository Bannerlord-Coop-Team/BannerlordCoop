using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Patches;

[HarmonyPatch(typeof(TroopRoster))]
internal class RemoveTroopPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<RemoveTroopPatches>();

    [HarmonyPatch(nameof(TroopRoster.RemoveTroop))]
    [HarmonyPrefix]
    public static void PrefixRemoveTroop(TroopRoster __instance, CharacterObject troop, int numberToRemove, int xp)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to remove from a managed {type}", typeof(TroopRoster));
            return;
        }

        var message = new TroopRemoved(__instance, troop, numberToRemove, xp);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
