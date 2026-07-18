using Common;
using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MobilePartyAIs.Patches;

[HarmonyPatch(typeof(Campaign))]
internal class PartiesThinkPatch
{
    // TODO move to config
    private const int UPDATES_PER_TICK = 100;
    private const int TICK_DELAY_MS = 100;

    private static Task delay = Task.CompletedTask;

    private static int CurrentStartIdx = 0;

    private static readonly ILogger Logger = LogManager.GetLogger<PartiesThinkPatch>();

    [HarmonyPatch("PartiesThink")]
    [HarmonyPrefix]
    private static bool PartiesThinkPrefix(Campaign __instance, ref float dt)
    {
        if (ModInformation.IsClient) return false;

        if (delay.IsCompleted == false) return false;

        delay = Task.Delay(TICK_DELAY_MS);

        if (__instance.MobileParties.Count == 0) return false;

        var currentStartIdx = CurrentStartIdx % __instance.MobileParties.Count;
        CurrentStartIdx = (currentStartIdx + UPDATES_PER_TICK) % __instance.MobileParties.Count;

        for (int i = 0; i < UPDATES_PER_TICK; i++)
        {
            if (__instance.MobileParties.Count == 0) break;

            var currentIdx = (currentStartIdx + i) % __instance.MobileParties.Count;
            var mobileParty = __instance.MobileParties[currentIdx];
            var ai = mobileParty?.Ai;

            if (ai == null) continue;

            try
            {
                ai.Tick(dt);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Failed to tick mobile party AI for {MobilePartyId}", mobileParty.StringId);
            }
        }

        return false;
    }
}
