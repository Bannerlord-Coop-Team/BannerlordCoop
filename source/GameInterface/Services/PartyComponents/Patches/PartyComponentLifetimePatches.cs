using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Patches;

[HarmonyPatch]
internal class LordPartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<LordPartyComponentLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(LordPartyComponent));

    private static bool Prefix(LordPartyComponent __instance)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MobileParty), Environment.StackTrace);
            return true;
        }

        var message = new PartyComponentCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}

[HarmonyPatch]
internal class BanditPartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<BanditPartyComponentLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(BanditPartyComponent));

    private static bool Prefix(BanditPartyComponent __instance)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MobileParty), Environment.StackTrace);
            return true;
        }

        var message = new PartyComponentCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}

[HarmonyPatch]
internal class CaravanPartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<CaravanPartyComponentLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(CaravanPartyComponent));

    private static bool Prefix(CaravanPartyComponent __instance)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MobileParty), Environment.StackTrace);
            return true;
        }

        var message = new PartyComponentCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}

[HarmonyPatch]
internal class CustomPartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<CustomPartyComponentLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(CustomPartyComponent));

    private static bool Prefix(CustomPartyComponent __instance)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MobileParty), Environment.StackTrace);
            return true;
        }

        var message = new PartyComponentCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}

[HarmonyPatch]
internal class GarrisonPartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<GarrisonPartyComponentLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(GarrisonPartyComponent));

    private static bool Prefix(GarrisonPartyComponent __instance)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MobileParty), Environment.StackTrace);
            return true;
        }

        var message = new PartyComponentCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}

[HarmonyPatch]
internal class MilitiaPartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MilitiaPartyComponentLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(MilitiaPartyComponent));

    private static bool Prefix(MilitiaPartyComponent __instance)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MobileParty), Environment.StackTrace);
            return true;
        }

        var message = new PartyComponentCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}

[HarmonyPatch]
internal class VillagerPartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillagerPartyComponentLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(VillagerPartyComponent));

    private static bool Prefix(VillagerPartyComponent __instance)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MobileParty), Environment.StackTrace);
            return true;
        }

        var message = new PartyComponentCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}