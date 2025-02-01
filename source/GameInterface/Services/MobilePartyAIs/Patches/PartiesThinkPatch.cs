using HarmonyLib;
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

    [HarmonyPatch("PartiesThink")]
    [HarmonyPrefix]
    private static bool PartiesThinkPrefix(Campaign __instance, ref float dt)
    {
        if (delay.IsCompleted == false) return false;

        delay = Task.Delay(TICK_DELAY_MS);

        for (int i = 0; i < UPDATES_PER_TICK; i++)
        {
            var currentIdx = (CurrentStartIdx + i) % __instance.MobileParties.Count;

            __instance.MobileParties[currentIdx].Ai.Tick(dt);
        }

        CurrentStartIdx = (CurrentStartIdx + UPDATES_PER_TICK) % __instance.MobileParties.Count;

        return false;
    }
}
