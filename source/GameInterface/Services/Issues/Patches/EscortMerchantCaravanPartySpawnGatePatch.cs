using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Real, independently-verified client-authority gap (same shape flagged in the task brief for every other
/// Group A party-spawn gate): <c>EscortMerchantCaravanIssueQuest.SpawnCaravan()</c> - a separate, individually-
/// Harmony-patchable private method called from the real <c>QuestAcceptedConsequences()</c> Consequence (a live
/// <c>OfferDialogFlow.Consequence</c>, Category B - only ever reached on the genuine accepter's own machine) -
/// calls <c>CustomPartyComponent.CreateCustomPartyWithTroopRoster(...)</c>, which is hard-blocked on a client by
/// <c>CustomPartyComponentLifetimePatches</c> (confirmed by direct read, same shape as Smugglers/Merchant Army
/// of Poachers/Gang Leader Needs Weapons' guards party). On a client-accepter, the resulting
/// <c>_questCaravanMobileParty</c> would be a broken, orphaned ghost only that client can see, while the quest
/// is permanently stuck for everyone else.
///
/// Unlike Caravan Ambush, <c>QuestAcceptedConsequences()</c> itself is NOT gated whole - only <c>SpawnCaravan()</c>
/// is, because <c>StartQuest()</c> (so <c>RegisterEvents()</c> activates on the genuine accepter, not the
/// server) and the activation journal log both must stay local to whichever machine genuinely accepted, exactly
/// like every other Category-B accept flow in this family, and are safe to run unmodified everywhere (no
/// captured/forced state needed for either).
///
/// Unlike every other party-spawn gate in this family, <c>SpawnCaravan()</c> needs NO captured/forwarded
/// accepter-derived value at all - see <see cref="IEscortMerchantCaravanIssueInterface"/>'s type doc comment for
/// the full derivation (it reads only <c>base.QuestGiver</c>-derived state, never <c>MobileParty.MainParty</c>).
/// That is what makes <see cref="IEscortMerchantCaravanIssueInterface.SpawnCaravanOnServer"/> a bare reflective
/// invoke of the real method rather than a hand-reimplementation, and what makes this Postfix (not a separate
/// broadcast call in <see cref="Handlers.EscortMerchantCaravanIssueHandler"/>) the single choke point covering
/// both a genuine host-owner accept AND a client-owner's forwarded request being serviced.
/// </summary>
[HarmonyPatch(typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest))]
internal class EscortMerchantCaravanPartySpawnGatePatch
{
    [HarmonyPatch("SpawnCaravan")]
    [HarmonyPrefix]
    private static bool Prefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance)
    {
        if (!ModInformation.IsClient) return true;

        // Client: the real body would build a broken CustomPartyComponent plus an orphaned, never-synced
        // MobileParty ghost (see the type doc comment). Forward the spawn to the server instead of running the
        // real body locally.
        var owner = __instance.QuestGiver;
        if (owner == null) return false;

        MessageBroker.Instance.Publish(__instance, new EscortMerchantCaravanPartySpawnRequested(owner));

        return false;
    }

    [HarmonyPatch("SpawnCaravan")]
    [HarmonyPostfix]
    private static void Postfix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance)
    {
        if (ModInformation.IsClient) return;

        // Genuine server-side creation - either the host itself is the owner (vanilla's own
        // QuestAcceptedConsequences ran this unmodified) or the server is servicing a client's forwarded
        // request via IEscortMerchantCaravanIssueInterface.SpawnCaravanOnServer's reflective replay - both paths
        // reach this SAME real, Harmony-postfixed method, so one broadcast point covers both (see that
        // interface's own type doc comment).
        ContainerProvider.TryResolve<IEscortMerchantCaravanIssueInterface>(out var issueInterface);
        if (issueInterface == null) return;

        var owner = __instance.QuestGiver;
        if (!issueInterface.TryCaptureCaravanParty(owner, out var caravanParty)) return;

        MessageBroker.Instance.Publish(__instance, new EscortMerchantCaravanPartySpawned(owner, caravanParty));
    }
}
