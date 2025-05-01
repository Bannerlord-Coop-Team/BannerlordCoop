using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Patches.Lifetime;


/// <summary>
/// Harmony patches for the lifetime of a <see cref="CaravanPartyComponent"/> object
/// </summary>
[HarmonyPatch]
internal class CaravanPartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<CaravanPartyComponentLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(CaravanPartyComponent));

    private static bool Prefix(CaravanPartyComponent __instance)
    {
        // Call original if we call this function
        if (CallPolicy.IsOriginalAllowed()) return true;

        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

        var message = new PartyComponentCreated(__instance);

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(__instance, message);

        return true;
    }
}