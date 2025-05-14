using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Stances.Messages.Lifetime;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Stances.Patches;

/// <summary>
/// Patches required for creating a StanceLink
/// </summary>
[HarmonyPatch]
internal class StanceLinkPatches
{
    private static ILogger Logger = LogManager.GetLogger<Kingdom>();

    [HarmonyPatch(typeof(StanceLink), MethodType.Constructor, typeof(StanceType), typeof(IFaction), typeof(IFaction), typeof(bool))]
    [HarmonyPrefix]
    private static bool CreateStanceLinkPrefix(ref StanceLink __instance, StanceType stanceType, IFaction faction1, IFaction faction2, bool isAtConstantWar)
    {
        // Call original if we call this function
        if (CallPolicy.IsOriginalAllowed()) return true;

        if (CallPolicy.SkipIfClient(Logger, out var result)) return result;


        var message = new StanceLinkCreated(__instance, stanceType, faction1, faction2, isAtConstantWar);

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(__instance, message);


        return true;
    }
}
