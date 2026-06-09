using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches for lifecycle of <see cref="MobileParty"/> objects.
/// </summary>
[HarmonyPatch(typeof(MobileParty))]
internal class PartyLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyLifetimePatches>();

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPostfix]
    private static void PostfixCtor(MobileParty __instance)
    {
        Logger.Debug("[{Instance}] MobileParty created with name {partyName}, {StringId}", __instance, __instance.Name, __instance.StringId);
    }
}

[HarmonyPatch(typeof(DestroyPartyAction))]
internal class DestroyPartyActionPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<DestroyPartyActionPatch>();
    [HarmonyPatch(nameof(DestroyPartyAction.Apply))]
    [HarmonyPrefix]
    private static bool PrefixApply(PartyBase destroyerParty, MobileParty destroyedParty)
    {

        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Never destroy a party owned by a connected player. On the server a remote player's party
        // is NOT MobileParty.MainParty, so vanilla's main-party guard does not protect it; a lost/
        // finalized MapEvent (MapEventSide.HandleMapEventEndForPartyInternal) would call
        // DestroyPartyAction.Apply on MobileParty_Player and remove it from the object manager.
        // The party then no longer resolves and a subsequent settlement encounter activates an
        // empty/unregistered menu -> null GameMenu NRE in MenuContext.HandleStates.
        // Blocking here also prevents publishing DestroyPartyApplied, so clients keep the party too.
        if (destroyedParty != null && destroyedParty.IsPlayer())
        {
            Logger.Warning("Blocked DestroyPartyAction for player party {partyName}, {StringId}", destroyedParty.Name, destroyedParty.StringId);
            return false;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to apply DestroyPartyAction for party {partyName}, {StringId}", destroyedParty.Name, destroyedParty.StringId);
            return true;
        }

        var message = new DestroyPartyApplied(destroyerParty, destroyedParty);
        MessageBroker.Instance.Publish(null, message);
        return true;
    }

    [HarmonyPatch(nameof(DestroyPartyAction.ApplyForDisbanding))]
    [HarmonyPrefix]
    private static void PrefixApplyForDisbanding(MobileParty disbandedParty, Settlement relatedSettlement)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to apply DestroyPartyAction for disbanding party {partyName}, {StringId}", disbandedParty.Name, disbandedParty.StringId);
            return;
        }

        var message = new PartyDisbanded(disbandedParty, relatedSettlement);
        MessageBroker.Instance.Publish(null, message);
    }
}