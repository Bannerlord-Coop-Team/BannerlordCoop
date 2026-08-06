using System;
using Common;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic;

// Lets the SERVER's own authoritative CompleteIssueWithAlternativeSolution call bypass the ownership-gate
// Prefixes (GenericQuestTypeAlternativeSolutionOwnershipGatePatch / NewIssueTypesAlternativeSolutionOwnershipGatePatch):
// IsLocalPeerOwner compares against ControllerIdProvider's own LOCAL platform id, which a dedicated server's
// own id can never equal a connected client's, so without this the server could never run the real
// completion for a client-owned quest at all (see CompleteOnServer's own doc comment for why that matters).
public sealed class AlternativeSolutionCompletionAuthorityGuard : IDisposable
{
    [ThreadStatic]
    private static int _count;

    public AlternativeSolutionCompletionAuthorityGuard() => _count++;

    public void Dispose() => _count = _count > 0 ? _count - 1 : 0;

    public static bool IsActive => _count > 0;
}

public static class AlternativeSolutionCompletionRunner
{
    // Called from whichever peer's own HourlyTick determines it is the recorded owner. On a client this only
    // sends a request rather than completing directly: CompleteIssueWithAlternativeSolution rolls an unseeded
    // MBRandom check and grants every reward/relationship/troop-XP/casualty consequence inline, so running it
    // client-side would make that peer's own local RNG state and campaign-state writes the sole, unmirrored
    // source of truth for the outcome instead of the server.
    public static bool TryTriggerOwnedCompletion(Hero owner, Action<Hero> requestServerCompletion)
    {
        if (owner?.Issue is not IssueBase issue) return false;
        if (!issue.IsSolvingWithAlternative || !issue.AlternativeSolutionReturnTimeForTroops.IsPast) return false;
        if (!IssueOwnershipRegistry.IsLocalPeerOwner(owner)) return false;

        if (ModInformation.IsServer)
        {
            CompleteOnServer(owner, issue);
        }
        else
        {
            requestServerCompletion(owner);
        }

        return true;
    }

    // The server-side counterpart of TryTriggerOwnedCompletion's request branch - called either directly above
    // (the owner IS the server, e.g. a listen-server host) or from a validated per-type request handler.
    public static void CompleteOnServer(Hero owner, IssueBase issue)
    {
        IssueManagerQuestCompletedReasonCapture.PendingReasons[owner] = VillageIssueFinalizeReason.AlternativeSolutionSuccess;

        using (new AlternativeSolutionCompletionAuthorityGuard())
        {
            issue.CompleteIssueWithAlternativeSolution();
        }
    }
}
