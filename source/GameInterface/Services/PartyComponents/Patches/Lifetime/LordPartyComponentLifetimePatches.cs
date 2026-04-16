using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.Patches.Lifetime;


/// <summary>
/// Harmony patches for the lifetime of a <see cref="LordPartyComponent"/> object
/// </summary>
[HarmonyPatch]
internal class LordPartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<LordPartyComponentLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(LordPartyComponent));

    private static bool Prefix(LordPartyComponent __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", typeof(LordPartyComponent));
            return true;
        }

        var message = new PartyComponentCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

}

/// <summary>
/// Guards against NullReferenceException in <see cref="LordPartyComponent.HomeSettlement"/>
/// when <see cref="LordPartyComponent.Owner"/> is null during a multiplayer sync transition.
/// </summary>
[HarmonyPatch(typeof(LordPartyComponent), nameof(LordPartyComponent.HomeSettlement), MethodType.Getter)]
internal class LordPartyComponentHomeSettlementPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<LordPartyComponentHomeSettlementPatch>();

    [HarmonyPrefix]
    private static bool Prefix(LordPartyComponent __instance, ref Settlement __result)
    {
        if (__instance.Owner == null)
        {
            Logger.Debug("LordPartyComponent.HomeSettlement accessed with null Owner (MobileParty: {Party}, IsClient: {IsClient})",
                __instance.MobileParty?.StringId ?? "null",
                ModInformation.IsClient);
            __result = null;
            return false;
        }
        return true;
    }
}