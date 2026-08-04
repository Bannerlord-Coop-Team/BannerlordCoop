using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Real, independently-verified bug (found while implementing this quest, matching the "known facts" bullet
/// this task was seeded with, confirmed directly against decompiled source rather than trusted blindly):
/// <c>SmugglersIssueQuest.QuestAcceptedConsequences()</c> - a live <c>OfferDialogFlow.Consequence</c>, only
/// ever reached on the genuine accepter's own machine (Category B in
/// doc/GroupA_HostileMobilePartySync_Design_v3.md §5) - calls the private <c>CreateSmugglerParty()</c>, which
/// calls <c>CustomPartyComponent.CreateCustomPartyWithTroopRoster(...)</c>. That factory's inner
/// <c>new CustomPartyComponent(...)</c> is HARD-BLOCKED on a client
/// (<c>CustomPartyComponentLifetimePatches.Prefix</c> returns false there, so none of the component's fields -
/// including <c>_initializationArgs</c> - ever get set, meaning <c>OnMobilePartySetOnCreation</c> later no-ops
/// and the party never gets its <c>ActualClan</c>/position/roster wired through the component). The
/// <c>MobileParty</c> itself is NOT blocked by that same patch, but IS silently orphaned by
/// <c>GameInterface.Registry.Auto.LifetimePatches&lt;MobileParty&gt;.CreatePrefix</c> - a void-returning method
/// that only logs "Client created managed MobileParty" on a client and lets the constructor run anyway,
/// meaning the resulting party never gets an id/never syncs to any other peer. Net effect on a client-accepter:
/// a broken, split-brain "ghost" smuggler party only that one client can ever see, while the quest is
/// permanently stuck for everyone else. §5's Category B reasoning ("MainParty already correctly means this
/// machine's own party wherever it executes") is correct about WHAT MainParty means, but doesn't cover this
/// separate construction-time gate - the design doc's own §4 (roster ordering) carried Smugglers' party-spawn
/// method forward as "not independently re-verified this pass... cheap spot-check at implementation time",
/// and this IS that spot-check turning up a real gap, the same shape flagged for verification going into this
/// task.
///
/// Fix: gate <c>CreateSmugglerParty</c> itself - allow it unmodified on the server (where
/// <c>CustomPartyComponentLifetimePatches</c> never blocks it, so vanilla's own behavior is completely
/// unchanged for a host who is the genuine accepter), and on a client, skip the broken local call entirely and
/// forward the request to the server instead - server-authoritative, block-and-forward-as-request on a client,
/// allow on server. See <see cref="Interfaces.ISmugglersIssueInterface"/>'s type doc comment for the full
/// mechanism, including why <c>desiredMenCount</c>/<c>customPartyBaseSpeed</c> (both derived from
/// <c>MobileParty.MainParty</c> inside the real method) are captured HERE, on the accepter's own machine,
/// before forwarding - the server's own MainParty would be the wrong composition whenever the genuine
/// accepter is a remote client.
/// </summary>
[HarmonyPatch(typeof(SmugglersIssueBehavior.SmugglersIssueQuest))]
internal class SmugglersPartySpawnGatePatch
{
    [HarmonyPatch("CreateSmugglerParty")]
    [HarmonyPrefix]
    private static bool Prefix(SmugglersIssueBehavior.SmugglersIssueQuest __instance, ref MobileParty __result)
    {
        if (!ModInformation.IsClient) return true;

        // Client: the real body would build a broken CustomPartyComponent plus an orphaned, never-synced
        // MobileParty (see the type doc comment). Skip it locally.
        __result = null;

        var mainParty = MobileParty.MainParty;
        if (mainParty == null || __instance.QuestGiver == null) return false;

        // Same formula as the real (now-skipped) method body - captured from THIS machine's own genuinely
        // correct MainParty before forwarding, since the server's own MainParty would be the wrong party's
        // composition whenever this accepter is a remote client.
        var desiredMenCount = (int)MathF.Clamp(MathF.Ceiling(mainParty.MemberRoster.TotalManCount * 0.8f), 15f, 35f);
        var customPartyBaseSpeed = mainParty.Speed * 1.1f;

        MessageBroker.Instance.Publish(__instance,
            new SmugglersPartySpawnRequested(__instance.QuestGiver, desiredMenCount, customPartyBaseSpeed));

        return false;
    }

    [HarmonyPatch("CreateSmugglerParty")]
    [HarmonyPostfix]
    private static void Postfix(SmugglersIssueBehavior.SmugglersIssueQuest __instance, MobileParty __result)
    {
        if (ModInformation.IsClient || __result == null) return;

        // Genuine server-side creation (the host's own live accept, unmodified) - broadcast so every OTHER
        // peer's mirror force-writes _smugglerParty to reference this same, already-AutoRegistry-synced
        // party. The client-forwarded path (Handlers.SmugglersIssueHandler.Handle_RequestSmugglerPartySpawn)
        // never reaches this method at all - it calls ISmugglersIssueInterface.CreateReplicatedSmugglerParty
        // directly and publishes the same SmugglersPartySpawned event itself.
        MessageBroker.Instance.Publish(__instance, new SmugglersPartySpawned(__instance.QuestGiver, __result));
    }
}
