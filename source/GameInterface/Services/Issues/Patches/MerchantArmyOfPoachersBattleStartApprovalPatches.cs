using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Task item 3 ("apply the same BattleStartApprovalPatches-style server-arbitrated start mechanism Gang Leader
/// Needs Weapons built"): <c>StartQuestBattle()</c>'s own mission-launch calls
/// (<c>PlayerEncounter.RestartPlayerEncounter</c>/<c>StartBattle</c>/<c>CampaignMission.OpenBattleMission</c>)
/// are LOCAL, UI/mission-launch APIs that only ever make sense on the genuine owner's own machine - unlike a
/// party spawn, there is no "the server does it instead" relocation available, and this quest's scripted
/// 1-vs-poachersParty battle is explicitly out of scope for the separately-tracked
/// <c>BattleMissionEntryPatch</c>-pipeline gap (see doc/GroupA_HostileMobilePartySync_Design_v3.md §6 and this
/// task's own scope note - <c>StartQuestBattle</c> calls the
/// <c>CampaignMission.OpenBattleMission(string, bool, string)</c> overload, a different overload from the one
/// that pipeline already covers).
///
/// Unlike Gang Leader Needs Weapons, <c>StartQuestBattle()</c> is a named, directly Harmony-patchable
/// <c>internal</c> method - reached from TWO real call sites, both on <c>MerchantArmyOfPoachersIssueBehavior</c>
/// itself: <c>engage_poachers_consequence</c> (the "Fight the poachers" game-menu option) and
/// <c>army_of_poachers_village_on_init</c>'s own poll of <c>_talkedToPoachersBattleWillStart</c> (set by a
/// dialogue option). Both call sites are themselves only reachable via the <c>army_of_poachers_village</c> menu,
/// itself only ever switched-to by this quest's own <c>RegisterEvents()</c>-wired <c>HourlyTick</c>/
/// <c>GameMenuOpened</c> listeners - Category A per design doc §5, so (unlike Gang Leader Needs Weapons' shared
/// guard dialogue) no non-owner peer can reach this method today. Gated anyway, defense-in-depth, matching this
/// session's established "gate the actual mutation" rule, and to close the "whichever peer's menu happens to
/// open first" race the task's own known-facts section describes for the general shape of this bug family.
///
/// Fix shape: ownership-gate <c>StartQuestBattle()</c> outright for anyone who isn't the recorded owner. For the
/// genuine owner: if this machine is the host/server, vanilla runs completely unmodified (zero divergence). If
/// this machine is a remote client, the real body is blocked and a request is forwarded to the server instead -
/// the server is the single arbiter of WHETHER the fight may start; on approval, the OWNER's own machine
/// (whichever machine that genuinely is) is the one that actually invokes the real <c>StartQuestBattle()</c> -
/// see <see cref="Handlers.MerchantArmyOfPoachersIssueHandler.Handle_NetworkBattleApproved"/> and
/// <see cref="Interfaces.IMerchantArmyOfPoachersIssueInterface.InvokeRealStartQuestBattle"/>'s doc comment for
/// why that call is wrapped in <see cref="Common.Util.AllowedThread"/> to step around this same gate.
/// </summary>
[HarmonyPatch(typeof(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest))]
internal class MerchantArmyOfPoachersBattleStartApprovalPatches
{
    [HarmonyPatch("StartQuestBattle")]
    [HarmonyPrefix]
    private static bool StartQuestBattlePrefix(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest __instance)
    {
        // The owner's own already-approved invocation (IMerchantArmyOfPoachersIssueInterface.InvokeRealStartQuestBattle,
        // wrapped in AllowedThread) - let the real body run.
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var owner = __instance.QuestGiver;
        if (owner == null) return true;

        // Never a non-owner's business.
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(owner)) return false;

        // Real owner, host/server: vanilla runs completely unmodified.
        if (!ModInformation.IsClient) return true;

        // Real owner, remote client: the mission-launch itself must stay local to this machine (see the type
        // doc comment), but WHETHER it's allowed to start is server-arbitrated instead of racing unchecked.
        MessageBroker.Instance.Publish(__instance, new MerchantArmyOfPoachersBattleStartRequested(owner));
        return false;
    }

    [HarmonyPatch("StartQuestBattle")]
    [HarmonyPostfix]
    private static void StartQuestBattlePostfix(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest __instance)
    {
        // The owner-client's own already-approved invocation already came FROM an approval broadcast - do not
        // publish a second one.
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (ModInformation.IsClient) return;

        // Genuine host-owner's own real StartQuestBattle() just ran, vanilla unmodified - broadcast so every
        // OTHER peer's mirror flips its own local bookkeeping flag too (parity-only, not load-bearing - see
        // IMerchantArmyOfPoachersIssueInterface.ForceIsReadyToBeFinalized's doc comment).
        var owner = __instance.QuestGiver;
        MessageBroker.Instance.Publish(__instance, new MerchantArmyOfPoachersBattleApproved(owner));
    }
}
