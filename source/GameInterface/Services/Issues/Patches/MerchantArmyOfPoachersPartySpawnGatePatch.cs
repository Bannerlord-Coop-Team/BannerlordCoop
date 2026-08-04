using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// The task's own decisive, confirmed bug: vanilla creates <c>_poachersParty</c> lazily, inside the
/// <c>army_of_poachers_village</c> game-menu's <c>on_init</c> callback
/// (<c>if (Instance._poachersParty == null &amp;&amp; !Hero.MainHero.IsWounded) Instance.CreatePoachersParty();</c>)
/// - whichever peer's own client first happens to open that menu (itself gated on purely local night-time/
/// <c>PlayerEncounter.IsPlayerWaiting</c> state, not any authoritative trigger) creates the world-visible party.
/// There is no gameplay reason to wait for a menu-open at all - decompiled source shows the party just sits
/// parked, AI-disabled, from the moment the quest starts.
///
/// Fixed by moving the spawn call itself off the menu callback entirely, onto the accept-time flow: this Postfix
/// runs immediately after the real, unmodified <c>QuestAcceptedConsequences()</c> body
/// (<c>StartQuest()</c>/<c>AddLog</c>/<c>AddTrackedObject</c> - a live <c>OfferDialogFlow.Consequence</c>,
/// Category B in doc/GroupA_HostileMobilePartySync_Design_v3.md §5, only ever reached on the genuine accepter's
/// own machine, whichever machine that is), and triggers the poachers-party spawn - gated the same way as every
/// other party-spawn gate this session (block-and-forward-as-request on a client-owner, allow on server/
/// host-owner, per Smugglers/Caravan Ambush/Gang Leader Needs Weapons precedent). <c>CustomPartyComponent</c>'s
/// ctor is hard-blocked on a client (<see cref="GameInterface.Services.PartyComponents.Patches.Lifetime.CustomPartyComponentLifetimePatches"/>),
/// same shape as every other custom-party-spawning quest in this family - unlike them, though, this quest never
/// called the party-construction method from a live dialogue/menu Consequence at all in vanilla, so there is no
/// existing vanilla call site to gate; this Postfix IS the new trigger.
///
/// Vanilla's own <c>army_of_poachers_village_on_init</c> null-check is left completely untouched - not patched,
/// not deleted. It becomes permanently-false dead code once <c>_poachersParty</c> always already exists by the
/// time that menu could ever open (which, per design doc §5 Category A, only the genuine owner's own machine can
/// ever reach in the first place), which is harmless and not worth a separate patch.
///
/// See <see cref="Interfaces.IMerchantArmyOfPoachersIssueInterface"/>'s type doc comment for why
/// <see cref="Interfaces.IMerchantArmyOfPoachersIssueInterface.CreatePoachersPartyOnServer"/> is a faithful,
/// documented reimplementation rather than a bare reflective invoke of vanilla's own private method.
/// </summary>
[HarmonyPatch(typeof(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest))]
internal class MerchantArmyOfPoachersPartySpawnGatePatch
{
    [HarmonyPatch("QuestAcceptedConsequences")]
    [HarmonyPostfix]
    private static void QuestAcceptedConsequencesPostfix(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest __instance)
    {
        // Only ever reached on the genuine accepter's own machine (Category B - see the type doc comment).
        var owner = __instance.QuestGiver;
        if (owner == null) return;

        ContainerProvider.TryResolve<IMerchantArmyOfPoachersIssueInterface>(out var issueInterface);
        if (issueInterface == null) return;

        // Idempotent defensive guard - should never be true this early, but mirrors this family's own
        // established convention of never re-creating an already-existing party.
        if (issueInterface.TryCapturePoachersParty(owner, out _)) return;

        if (!ModInformation.IsClient)
        {
            // Host/server-owner: safe to build the real party directly, right now - no CustomPartyComponent
            // block applies here.
            var party = issueInterface.CreatePoachersPartyOnServer(owner);
            if (party != null)
            {
                MessageBroker.Instance.Publish(__instance, new MerchantArmyOfPoachersPartySpawned(owner, party));
            }
            return;
        }

        // Remote client-owner: CustomPartyComponent's ctor is hard-blocked here (same shape as Smugglers/
        // Caravan Ambush/Gang Leader Needs Weapons) - forward a request to the server instead.
        MessageBroker.Instance.Publish(__instance, new MerchantArmyOfPoachersPartySpawnRequested(owner));
    }
}
