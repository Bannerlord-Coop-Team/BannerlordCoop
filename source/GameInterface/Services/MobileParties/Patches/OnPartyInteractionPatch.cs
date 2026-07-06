using Common;
using Common.Logging;
using GameInterface.Services.MobileParties.Handlers;
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

        if (ContainerProvider.TryResolve<OnPartyInteractionHandler>(out var handler) &&
            handler.TryHandleReciprocalPlayerInteraction(targetParty, engagingParty))
            return false;

        return true;
    }

    internal static MobileParty GetEffectiveInteractionTargetParty(MobileParty targetParty, MobileParty engagingParty)
    {
        if (targetParty?.AttachedTo != null && engagingParty != targetParty.AttachedTo)
            return targetParty.AttachedTo;

        return targetParty;
    }
}
