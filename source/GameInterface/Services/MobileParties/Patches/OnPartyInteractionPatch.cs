using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Skips <see cref="MobileParty.OnPartyInteraction"/> when the party has no members.
/// An empty <see cref="MobileParty.MemberRoster"/> causes the original method to
/// operate on a party that has effectively been emptied/destroyed.
/// </summary>
[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.OnPartyInteraction))]
internal class OnPartyInteractionPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<OnPartyInteractionPatch>();

    [HarmonyPrefix]
    private static bool Prefix(MobileParty __instance, MobileParty engagingParty)
    {
        var targetParty = GetEffectiveInteractionTargetParty(__instance, engagingParty);

        if (targetParty?.MemberRoster == null || targetParty.MemberRoster.Count == 0)
        {
            Logger.Verbose("Skipping {Method} for party '{Party}' because its MemberRoster is empty",
                nameof(MobileParty.OnPartyInteraction), targetParty?.StringId ?? "<null>");
            return false;
        }

        if (!CanHandleReciprocalPlayerInteraction(targetParty, engagingParty)) return true;
        if (!IsReciprocalPlayerInteractionReady(targetParty, engagingParty)) return true;
        if (!TryGetPartyBases(targetParty, engagingParty, out var targetPartyBase, out var engagingPartyBase))
            return true;

        var message = new ReciprocalPlayerPartyInteractionAttempted(targetPartyBase, engagingPartyBase);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    internal static MobileParty GetEffectiveInteractionTargetParty(MobileParty targetParty, MobileParty engagingParty)
    {
        if (targetParty?.AttachedTo != null && engagingParty != targetParty.AttachedTo)
            return targetParty.AttachedTo;

        return targetParty;
    }

    internal static bool CanHandleReciprocalPlayerInteraction(MobileParty targetParty, MobileParty engagingParty)
    {
        if (ModInformation.IsServer) return false;
        if (targetParty == null || engagingParty == null) return false;
        if (targetParty == engagingParty) return false;
        if (!engagingParty.IsMainParty) return false;
        if (!engagingParty.IsControlledByThisInstance()) return false;
        if (!targetParty.IsPlayerParty()) return false;

        return true;
    }

    internal static bool IsReciprocalPlayerInteractionReady(MobileParty targetParty, MobileParty engagingParty)
    {
        if (targetParty.CurrentSettlement != null) return false;
        if (targetParty.MapEvent != null || engagingParty.MapEvent != null) return false;
        if (!targetParty.IsEngaging) return false;
        if (targetParty.ShortTermTargetParty != engagingParty) return false;

        return true;
    }

    internal static bool TryGetPartyBases(
        MobileParty targetParty,
        MobileParty engagingParty,
        out PartyBase targetPartyBase,
        out PartyBase engagingPartyBase)
    {
        targetPartyBase = targetParty.Party;
        engagingPartyBase = engagingParty.Party;

        return targetPartyBase != null && engagingPartyBase != null;
    }
}