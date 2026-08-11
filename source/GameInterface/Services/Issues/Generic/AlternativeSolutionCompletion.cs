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

    public static void CompleteOnServer(Hero owner, IssueBase issue)
    {
        IssueManagerQuestCompletedReasonCapture.PendingReasons[owner] = IssueFinalizeReason.AlternativeSolutionSuccess;

        using (new AlternativeSolutionCompletionAuthorityGuard())
        using (ResolveTrueOwnerScope(owner))
        {
            issue.CompleteIssueWithAlternativeSolution();
        }
    }

    private static IDisposable ResolveTrueOwnerScope(Hero issueOwner)
    {
        if (!IssueOwnershipRegistry.TryGetOwnerControllerId(issueOwner, out var controllerId)) return NullScope.Instance;
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager)) return NullScope.Instance;
        if (!playerManager.TryGetPlayer(controllerId, out var player)) return NullScope.Instance;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return NullScope.Instance;
        if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var trueOwnerHero)) return NullScope.Instance;

        objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var trueOwnerParty);

        return new MainHeroSubstitutionScope(trueOwnerHero, trueOwnerParty);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose() { }
    }
}
