using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// When the Militia Is Set
/// </summary>
[HarmonyPatch(typeof(Settlement))]
public class MilitiaSettlementPatch
{
    private static ILogger Logger = LogManager.GetLogger<Settlement>();

    [HarmonyPatch(nameof(Settlement.Militia), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool MilitiaPrefix(out bool __state)
    {
        __state = false;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", typeof(Settlement));
            return true;
        }

        __state = true;
        return true;
    }

    [HarmonyPatch(nameof(Settlement.Militia), MethodType.Setter)]
    [HarmonyPostfix]
    private static void MilitiaPostfix(Settlement __instance, bool __state)
    {
        if (!__state) return;

        // Native has already moved whole militia into the separately synchronized troop roster.
        var message = new SettlementChangedMilitia(__instance, __instance._readyMilitia);
        MessageBroker.Instance.Publish(__instance, message);
    }

    internal static void RunMiltiaChange(Settlement settlement, float militia)
    {
        GameThread.Run(() =>
        {
            using (new AllowedThread())
            {
                settlement._readyMilitia = militia;
            }
        });
    }
}
