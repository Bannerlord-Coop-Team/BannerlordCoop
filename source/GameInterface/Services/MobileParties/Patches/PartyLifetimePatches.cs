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
    internal static bool PrefixApply(PartyBase destroyerParty, MobileParty destroyedParty)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            // A destroy that runs while patches are skipped can be a vanilla side effect nested
            // inside another replicated action (e.g. a settlement ownership change culling its
            // patrol). On the server that destroy is still authoritative and must replicate, or
            // clients keep a zombie party. The server never applies a received destroy, so this
            // cannot double-publish.
            if (CallOriginalPolicy.IsServerNestedCall())
            {
                // Nested destroys must not bypass the player-party protection either.
                if (IsProtectedPlayerParty(destroyedParty)) return false;

                PublishDestroyIfActive(destroyerParty, destroyedParty);
            }

            return true;
        }

        if (IsProtectedPlayerParty(destroyedParty)) return false;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to apply DestroyPartyAction for party {partyName}, {StringId}", destroyedParty.Name, destroyedParty.StringId);
            return true;
        }

        PublishDestroyIfActive(destroyerParty, destroyedParty);
        return true;
    }

    /// <summary>
    /// Never destroy a party owned by a connected player. On the server a remote player's party
    /// is NOT MobileParty.MainParty, so vanilla's main-party guard does not protect it; a lost/
    /// finalized MapEvent (MapEventSide.HandleMapEventEndForPartyInternal) would call
    /// DestroyPartyAction.Apply on MobileParty_Player and remove it from the object manager.
    /// The party then no longer resolves and a subsequent settlement encounter activates an
    /// empty/unregistered menu -> null GameMenu NRE in MenuContext.HandleStates.
    /// Blocking here also prevents publishing DestroyPartyApplied, so clients keep the party too.
    /// </summary>
    private static bool IsProtectedPlayerParty(MobileParty destroyedParty)
    {
        if (destroyedParty == null || !destroyedParty.IsPlayerParty()) return false;

        Logger.Warning("Blocked DestroyPartyAction for player party {partyName}, {StringId}", destroyedParty.Name, destroyedParty.StringId);
        return true;
    }

    private static void PublishDestroyIfActive(PartyBase destroyerParty, MobileParty destroyedParty)
    {
        // Only replicate the destruction of a live party. Vanilla can re-run a destroy the
        // replication layer already applied (the party is inactive by then), and replicating it
        // again would make clients double-apply it.
        if (destroyedParty == null || !destroyedParty.IsActive) return;

        MessageBroker.Instance.Publish(null, new DestroyPartyApplied(destroyerParty, destroyedParty));
    }

    [HarmonyPatch(nameof(DestroyPartyAction.ApplyForDisbanding))]
    [HarmonyPrefix]
    internal static void PrefixApplyForDisbanding(MobileParty disbandedParty, Settlement relatedSettlement)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            // Same as Apply above: a disband nested inside another replicated action on the
            // server is still authoritative and must replicate.
            if (CallOriginalPolicy.IsServerNestedCall())
                PublishDisbandIfActive(disbandedParty, relatedSettlement);

            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to apply DestroyPartyAction for disbanding party {partyName}, {StringId}", disbandedParty.Name, disbandedParty.StringId);
            return;
        }

        PublishDisbandIfActive(disbandedParty, relatedSettlement);
    }

    private static void PublishDisbandIfActive(MobileParty disbandedParty, Settlement relatedSettlement)
    {
        // Only replicate the disband of a live party. The enter-settlement prefix lets the
        // original action run after the handler chain already applied it, so vanilla reaches
        // this disband a second time with the party already removed; publishing again would
        // only produce duplicate-disband noise.
        if (disbandedParty == null || !disbandedParty.IsActive) return;

        MessageBroker.Instance.Publish(null, new PartyDisbanded(disbandedParty, relatedSettlement));
    }
}
