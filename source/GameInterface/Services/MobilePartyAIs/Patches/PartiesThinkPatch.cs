using Common;
using Common.Logging;
using GameInterface.Services.MobilePartyAIs;
using HarmonyLib;
using Serilog;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;

[HarmonyPatch(typeof(Campaign))]
internal static class PartiesThinkPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(PartiesThinkPatch));
    private static IPartyAiBatchRunner runner;

    internal static IPartyAiBatchRunner BoundRunner => Volatile.Read(ref runner);

    internal static void Bind(IPartyAiBatchRunner value) =>
        Interlocked.Exchange(ref runner, value);

    internal static void Unbind(IPartyAiBatchRunner value) =>
        Interlocked.CompareExchange(ref runner, null, value);

    [HarmonyPatch("PartiesThink")]
    [HarmonyPrefix]
    private static bool PartiesThinkPrefix(Campaign __instance, ref float dt)
    {
        if (ModInformation.IsClient)
        {
            if (MobileParty.MainParty.DefaultBehavior == AiBehavior.EscortParty)
                MobileParty.MainParty.Ai.Tick(dt);

            return false;
        }

        IPartyAiBatchRunner current = BoundRunner;
        if (current == null)
            return true;

        try
        {
            current.TickBatch(__instance, dt);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to tick mobile-party AI batch");
        }

        return false;
    }
}
