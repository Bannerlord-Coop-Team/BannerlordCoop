using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Data;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches for setting a party's army
/// </summary>
[HarmonyPatch(typeof(MobileParty))]
internal class PartyArmyPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyArmyPatches>();

    [HarmonyPatch(nameof(MobileParty.Army), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetArmyPrefix(MobileParty __instance, Army value)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Army), Environment.StackTrace);
            return true;
        }

        var data = new PartyArmyChangeData(__instance.StringId, value.GetStringId());
        var message = new PartyArmyChanged(data);
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    internal static void OverrideSetArmy(MobileParty mobileParty, Army army)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using(new AllowedThread())
            {
                mobileParty._army = army;
            }
        });
    }
}
