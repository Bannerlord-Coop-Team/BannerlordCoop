using GameInterface.Policies;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using Common.Messaging;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Syncs <see cref="MobileParty.SetMoveModeHold"/> for controlled parties.
/// </summary>
/// <remarks>
/// <see cref="MobileParty.SetMoveModeHold"/> clears a party's engage behavior by writing the AI fields directly
/// (<c>DefaultBehavior = Hold</c>, <c>TargetParty = null</c>, ...) instead of going through the synced
/// <see cref="MobilePartyAi.SetAiBehavior"/>. Vanilla calls it when a conversation/encounter is left
/// (e.g. <c>game_menu_encounter_meeting_on_init</c>) so the party stops targeting whatever it just talked to.
///
/// In coop that local-only write desyncs from the server: the server still believes the party is
/// <see cref="AiBehavior.EngageParty"/> (from the original engage order) and replicates that behavior back to the
/// client, which immediately re-runs the proximity encounter and re-opens the dialogue. Publishing a behavior change
/// here makes the hold authoritative so the server stops re-engaging.
/// </remarks>
[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.SetMoveModeHold))]
internal static class SetMoveModeHoldPatch
{
    [HarmonyPostfix]
    private static void Postfix(MobileParty __instance)
    {
        // Our own synced apply (AllowedThread) must not re-publish.
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        // Only the owner drives the change; replicas receive it through the behavior sync.
        if (__instance.IsPartyControlled() == false) return;

        // SetMoveModeHold has already cleared the target and set TargetPosition = Position.
        MessageBroker.Instance.Publish(__instance.Ai,
            new PartyBehaviorChangeAttempted(__instance.Ai, AiBehavior.Hold, null, __instance.TargetPosition));
    }
}
