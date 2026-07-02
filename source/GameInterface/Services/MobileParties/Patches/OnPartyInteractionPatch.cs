using Common;
using Common.Logging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
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
        if (__instance?.MemberRoster == null || __instance.MemberRoster.Count == 0)
        {
            Logger.Verbose("Skipping {Method} for party '{Party}' because its MemberRoster is empty",
                nameof(MobileParty.OnPartyInteraction), __instance?.StringId ?? "<null>");
            return false;
        }

        if (TryHandleReciprocalPlayerInteraction(__instance, engagingParty))
            return false;

        return true;
    }

    private static bool TryHandleReciprocalPlayerInteraction(MobileParty targetParty, MobileParty engagingParty)
    {
        if (!CanHandleReciprocalPlayerInteraction(targetParty, engagingParty)) return false;
        if (!IsReciprocalPlayerInteractionReady(targetParty, engagingParty)) return false;
        if (!TryGetPartyBases(targetParty, engagingParty, out var targetPartyBase, out var engagingPartyBase))
            return false;

        if (ShouldInitiateReciprocalPlayerInteraction(engagingPartyBase, targetPartyBase))
            EncounterManager.StartPartyEncounter(engagingPartyBase, targetPartyBase);

        return true;
    }

    private static bool CanHandleReciprocalPlayerInteraction(MobileParty targetParty, MobileParty engagingParty)
    {
        if (!ModInformation.IsClient) return false;
        if (targetParty == null || engagingParty == null) return false;
        if (targetParty == engagingParty) return false;
        if (!engagingParty.IsMainParty) return false;
        if (!engagingParty.IsControlledByThisInstance()) return false;
        if (!targetParty.IsPlayerParty()) return false;

        return true;
    }

    private static bool IsReciprocalPlayerInteractionReady(MobileParty targetParty, MobileParty engagingParty)
    {
        if (targetParty.CurrentSettlement != null) return false;
        if (targetParty.MapEvent != null || engagingParty.MapEvent != null) return false;
        if (!targetParty.IsEngaging) return false;
        if (targetParty.ShortTermTargetParty != engagingParty) return false;

        return true;
    }

    private static bool TryGetPartyBases(
        MobileParty targetParty,
        MobileParty engagingParty,
        out PartyBase targetPartyBase,
        out PartyBase engagingPartyBase)
    {
        targetPartyBase = targetParty.Party;
        engagingPartyBase = engagingParty.Party;

        return targetPartyBase != null && engagingPartyBase != null;
    }

    private static bool ShouldInitiateReciprocalPlayerInteraction(PartyBase engagingParty, PartyBase targetParty)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return false;
        if (!objectManager.TryGetId(engagingParty, out var engagingPartyId)) return false;
        if (!objectManager.TryGetId(targetParty, out var targetPartyId)) return false;

        return string.CompareOrdinal(engagingPartyId, targetPartyId) <= 0;
    }
}
