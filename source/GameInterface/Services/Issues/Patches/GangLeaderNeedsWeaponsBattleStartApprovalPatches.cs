using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Task item 4 ("BattleStartApprovalPatches"): <c>StartFight()</c>'s own mission-launch calls
/// (<c>PlayerEncounter.RestartPlayerEncounter</c>/<c>StartBattle</c>/
/// <c>CampaignMission.OpenBattleMissionWhileEnteringSettlement</c>) are LOCAL, UI/mission-launch APIs that only
/// ever make sense on the genuine owner's own machine - unlike a party spawn, there is no "the server does it
/// instead" relocation available (the server running this against ITS OWN, possibly-null-on-a-dedicated-server
/// <c>PartyBase.MainParty</c> would be nonsensical, and this quest's scripted 1-vs-guardsParty battle is
/// explicitly out of scope for the separately-tracked <c>BattleMissionEntryPatch</c>-pipeline gap - see
/// doc/GroupA_HostileMobilePartySync_Design_v3.md §6 and this task's own scope note).
///
/// What genuinely needs fixing: the dialogue option that sets <c>_startBattleMission = true</c> is an inline,
/// compiler-generated lambda Consequence with no stable name to Harmony-patch directly (unlike Caravan Ambush's
/// named Condition method), registered on EVERY peer's own mirror (<c>SetDialogs()</c> runs unconditionally
/// from the ctor) with no ownership check. Without a gate, any connected peer who talks to the shared
/// (correctly globally-mirrored, see <see cref="GangLeaderNeedsWeaponsGuardsPartySpawnGatePatch"/>) guardsParty
/// leader could flip THEIR OWN local <c>_startBattleMission</c> flag and have their own next "town" menu open
/// independently trigger a bogus local mission-launch attempt against a battle that isn't theirs - "whichever
/// peer's menu happens to open first" racing ahead with no arbiter. This gate catches the un-patchable lambda's
/// downstream effect indirectly by gating the NAMED, Harmony-patchable <c>StartFight()</c> method itself - the
/// one place <c>OnGameMenuOpened</c>'s own <c>_startBattleMission</c> poll actually acts on the flag.
///
/// Fix shape: ownership-gate <c>StartFight()</c> outright for anyone who isn't the recorded owner (their own
/// local <c>_startBattleMission</c> flag still gets reset to false by <c>OnGameMenuOpened</c>'s own code
/// BEFORE calling <c>StartFight()</c> - confirmed by direct read - so blocking just the real body here is a
/// clean, side-effect-free no-op for them, no separate flag cleanup needed). For the genuine owner: if this
/// machine is the host/server, vanilla runs completely unmodified (zero divergence, same as every other
/// owner-host path in this family) and the Postfix broadcasts an approval for parity. If this machine is a
/// remote client, the real body is blocked and a request is forwarded to the server instead - the server is
/// the single arbiter of WHETHER the fight may start, closing the race described above; on approval, the
/// OWNER's own machine (whichever machine that genuinely is) is the one that actually invokes the real
/// <c>StartFight()</c> - see <see cref="Handlers.GangLeaderNeedsWeaponsIssueHandler.Handle_NetworkBattleApproved"/>
/// and <see cref="IGangLeaderNeedsWeaponsIssueInterface.InvokeRealStartFight"/>'s doc comment for why that call
/// is wrapped in <see cref="Common.Util.AllowedThread"/> to step around this same gate.
/// </summary>
[HarmonyPatch(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest))]
internal class GangLeaderNeedsWeaponsBattleStartApprovalPatches
{
    [HarmonyPatch("StartFight")]
    [HarmonyPrefix]
    private static bool StartFightPrefix(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest __instance)
    {
        // The owner's own already-approved invocation (IGangLeaderNeedsWeaponsIssueInterface.InvokeRealStartFight,
        // wrapped in AllowedThread) - let the real body run.
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var owner = __instance.QuestGiver;
        if (owner == null) return true;

        // Never a non-owner's business - their own _startBattleMission flag was already reset to false by
        // OnGameMenuOpened's own code before reaching here, so blocking the real body is a clean no-op.
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(owner)) return false;

        // Real owner, host/server: vanilla runs completely unmodified.
        if (!ModInformation.IsClient) return true;

        // Real owner, remote client: the mission-launch itself must stay local to this machine (see the type
        // doc comment), but WHETHER it's allowed to start is server-arbitrated instead of racing unchecked.
        MessageBroker.Instance.Publish(__instance, new GangLeaderNeedsWeaponsBattleStartRequested(owner));
        return false;
    }

    [HarmonyPatch("StartFight")]
    [HarmonyPostfix]
    private static void StartFightPostfix(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest __instance)
    {
        // The owner-client's own already-approved invocation already came FROM an approval broadcast - do not
        // publish a second one.
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (ModInformation.IsClient) return;

        // Genuine host-owner's own real StartFight() just ran, vanilla unmodified - broadcast so every OTHER
        // peer's mirror flips its own local bookkeeping flag too (parity-only, not load-bearing - see
        // IGangLeaderNeedsWeaponsIssueInterface.ForceCheckForBattleResult's doc comment).
        var owner = __instance.QuestGiver;
        MessageBroker.Instance.Publish(__instance, new GangLeaderNeedsWeaponsBattleApproved(owner));
    }
}
