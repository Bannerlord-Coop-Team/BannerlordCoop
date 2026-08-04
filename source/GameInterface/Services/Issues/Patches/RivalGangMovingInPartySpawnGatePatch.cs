using Common;
using Common.Logging;
using GameInterface.Services.Issues.Handlers;
using HarmonyLib;
using SandBox.Issues;
using Serilog;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Real, independently-verified bug (the exact shape flagged going into this task, confirmed directly against
/// decompiled source rather than trusted blindly, for BOTH parties this quest spawns): <c>StartAlleyBattle()</c> -
/// a live <c>ConversationEndOneShot</c> consequence chain, only ever reached on the genuine accepter's own
/// machine (Category B, see doc/RivalGangMovingIn_Design_v2.md §6.1) - calls
/// <c>CreateRivalGangLeaderParty()</c> then <c>CreateAllyGangLeaderParty()</c>, each of which calls
/// <c>CustomPartyComponent.CreateCustomPartyWithTroopRoster(...)</c>. That factory's inner
/// <c>new CustomPartyComponent(...)</c> is HARD-BLOCKED on a client
/// (<c>CustomPartyComponentLifetimePatches.Prefix</c> returns false there), while the resulting
/// <c>MobileParty</c>s are NOT blocked but ARE silently orphaned (never registered/synced -
/// <c>GameInterface.Registry.Auto.LifetimePatches&lt;MobileParty&gt;.CreatePrefix</c> only logs on a client). Net
/// effect on a remote-client owner, unmitigated: two broken, split-brain ghost parties only that one client can
/// ever see, and the quest permanently stuck for everyone else - the same Smugglers-shaped bug, confirmed
/// present for BOTH parties here too.
///
/// Fix: gate BOTH methods. On the server (or a host who is the genuine owner), vanilla runs completely
/// unmodified. On a client, <c>CreateRivalGangLeaderParty()</c>'s own prefix issues ONE combined blocking
/// request (<see cref="RivalGangMovingInPartyCreationCoordinator.RequestBlocking"/>) that creates BOTH parties +
/// BOTH henchman Heroes server-side and force-writes all 4 quest fields before returning control - a fire-and-
/// forget request (the Smugglers/Caravan Ambush shape) does not work here, since <c>StartAlleyBattle()</c>
/// immediately dereferences <c>_rivalGangLeaderParty.Party</c> a few lines later. <c>CreateAllyGangLeaderParty()</c>'s
/// own prefix is then a pure no-op on a client - the combined request above already populated its fields too.
/// </summary>
[HarmonyPatch(typeof(RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest))]
internal class RivalGangMovingInPartySpawnGatePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<RivalGangMovingInPartySpawnGatePatch>();

    [HarmonyPatch("CreateRivalGangLeaderParty")]
    [HarmonyPrefix]
    private static bool RivalGangLeaderPrefix(RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest __instance)
    {
        if (!ModInformation.IsClient) return true; // host/server: vanilla unmodified, its own construction is unblocked

        var owner = __instance.QuestGiver;
        if (owner == null) return false;

        if (!ContainerProvider.TryResolve<RivalGangMovingInPartyCreationCoordinator>(out var coordinator))
        {
            Logger.Error("RivalGangMovingInPartyCreationCoordinator is not available; cannot request rival gang party creation");
            return false;
        }

        if (!coordinator.RequestBlocking(owner))
        {
            Logger.Error(
                "Failed to obtain server-created rival gang parties for {Owner}; the alley battle sequence will abort gracefully",
                owner.StringId);
        }

        return false;
    }

    [HarmonyPatch("CreateAllyGangLeaderParty")]
    [HarmonyPrefix]
    private static bool AllyGangLeaderPrefix()
    {
        // On a client, the combined request issued by RivalGangLeaderPrefix above already populated both
        // parties' fields (or left them null on failure) - nothing left to do either way.
        return !ModInformation.IsClient;
    }
}
