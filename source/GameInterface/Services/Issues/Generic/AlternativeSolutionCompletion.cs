using System;
using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic;

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
    public static bool TryTriggerOwnedCompletion(Hero owner, Action<Hero> requestServerCompletion)
    {
        if (owner?.Issue is not IssueBase issue) return false;
        if (!issue.IsSolvingWithAlternative || !issue.AlternativeSolutionReturnTimeForTroops.IsPast) return false;
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry) || !ownershipRegistry.IsLocalPeerOwner(owner)) return false;

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

    public static void CompleteOnServer(Hero owner, IssueBase issue)
    {
        using (new AlternativeSolutionCompletionAuthorityGuard())
        using (new IssueFinalizeAuthorityGuard())
        using (ResolveTrueOwnerScope(owner))
        {
            IssueManagerQuestCompletedReasonCapture.PendingReasons[owner] = IssueFinalizeReason.AlternativeSolutionSuccess;
            issue.CompleteIssueWithAlternativeSolution();
        }
    }

    private static MainHeroSubstitutionScope ResolveTrueOwnerScope(Hero issueOwner)
    {
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry) || !ownershipRegistry.TryGetOwnerControllerId(issueOwner, out var controllerId))
            throw new InvalidOperationException($"ResolveTrueOwnerScope: no recorded owner for issue owner {issueOwner}");
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            throw new InvalidOperationException("ResolveTrueOwnerScope: IPlayerManager is not resolvable");
        if (!playerManager.TryGetPlayer(controllerId, out var player))
            throw new InvalidOperationException($"ResolveTrueOwnerScope: no registered Player for controllerId {controllerId}");
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            throw new InvalidOperationException("ResolveTrueOwnerScope: IObjectManager is not resolvable");
        if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var trueOwnerHero))
            throw new InvalidOperationException($"ResolveTrueOwnerScope: could not resolve true owner Hero {player.HeroId}");

        objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var trueOwnerParty);

        return new MainHeroSubstitutionScope(trueOwnerHero, trueOwnerParty);
    }
}
