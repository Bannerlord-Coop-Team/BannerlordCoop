using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.Patches.BanditPartyComponents;

public enum BanditPartyComponentType
{
    Hideout,
    IsBossParty,
}

[HarmonyPatch(typeof(BanditPartyComponent))]
public class BanditPartyComponentPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<BanditPartyComponentPatches>();

    [HarmonyPatch(nameof(BanditPartyComponent.IsBossParty), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetIsBossPartyPrefix(BanditPartyComponent __instance, bool value)
    {
        // Call original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client changed unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(BanditPartyComponent), Environment.StackTrace);
            return false;
        }

        var message = new BanditPartyComponentUpdated(__instance, BanditPartyComponentType.IsBossParty, value.ToString());

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(nameof(BanditPartyComponent.Hideout), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetHideoutPrefix(BanditPartyComponent __instance, Hideout value)
    {
        // Call original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client changed unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(BanditPartyComponent), Environment.StackTrace);
            return false;
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return true;
        if (objectManager.TryGetId(value, out var hideoutId) == false) return true;

        var message = new BanditPartyComponentUpdated(__instance, BanditPartyComponentType.Hideout, hideoutId);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}