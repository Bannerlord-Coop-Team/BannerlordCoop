using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PartyVisuals.Messages;
using HarmonyLib;
using SandBox.View.Map;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;
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
                    if(i >= __instance._visualsFlattened.Count)
                    {
                        Logger.Warning("Index {index} was out of bounds for visuals flattened list of size {size}", i, __instance._visualsFlattened.Count);
                        continue;
                    }
                    __instance._visualsFlattened[i].Tick(dt, realDt, ref __instance._dirtyPartyVisualCount, ref __instance._dirtyPartiesList);
                }
            });
            for (int num = 0; num < __instance._dirtyPartyVisualCount + 1; num++)
            {
                __instance._dirtyPartiesList[num].ValidateIsDirty();
            }
            for (int num2 = __instance._fadingPartiesFlatten.Count - 1; num2 >= 0; num2--)
            {
                __instance._fadingPartiesFlatten[num2].TickFadingState(realDt, dt);
            }

            return false;
        }
    }
}
