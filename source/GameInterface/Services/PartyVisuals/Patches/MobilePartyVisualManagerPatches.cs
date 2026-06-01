using Common.Logging;
using HarmonyLib;
using SandBox.View.Map.Managers;
using Serilog;
using System;
using TaleWorlds.Library;

namespace GameInterface.Services.PartyVisuals.Patches
{
    [HarmonyPatch(typeof(MobilePartyVisualManager))]
    public class MobilePartyVisualManagerPatches
    {
        private static ILogger Logger = LogManager.GetLogger<MobilePartyVisualManagerPatches>();

        [HarmonyPatch(nameof(MobilePartyVisualManager.OnTick))]
        [HarmonyPrefix]
        private static bool Prefix(MobilePartyVisualManager __instance, float realDt, float dt)
        {
            __instance._dirtyPartyVisualCount = -1;
            TWParallel.For(0, __instance._visualsFlattened.Count, delegate (int startInclusive, int endExclusive)
            {
                for (int i = startInclusive; i < endExclusive; i++)
                {
                    if (i >= __instance._visualsFlattened.Count)
                    {
                        Logger.Warning("Index {index} was out of bounds for visuals flattened list of size {size}", i, __instance._visualsFlattened.Count);
                        continue;
                    }

                    try
                    {
                        __instance._visualsFlattened[i].Tick(dt, realDt, ref __instance._dirtyPartyVisualCount, ref __instance._dirtyPartiesList);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Failed to tick party visual");
                    }
                }
            });
            for (int num = 0; num < __instance._dirtyPartyVisualCount + 1; num++)
            {
                try
                {
                    __instance._dirtyPartiesList[num].ValidateIsDirty();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to validate is party visual dirty");
                }
            }
            for (int num2 = __instance._fadingPartiesFlatten.Count - 1; num2 >= 0; num2--)
            {
                try {
                    __instance._fadingPartiesFlatten[num2].TickFadingState(realDt, dt);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to tick fading state");
                }
            }

            return false;
        }
    }
}
