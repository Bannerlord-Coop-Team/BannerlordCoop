using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

/// <summary>The finalizer of every vanilla ending the server runs inside the owner's scope.</summary>
internal static class OwnerEndingFinalizer
{
    // Gives the scope back, or when the ending threw hands the issue to the ending coordinator and swallows the throw
    public static Exception Finish(IssueBase issue, IssueOutcome outcome, IDisposable owner, Exception exception)
    {
        var threwAsOwner = exception != null && owner != null;
        if (threwAsOwner && ContainerProvider.TryResolve<IIssueEndingCoordinator>(out var endings))
        {
            endings.EndAfterThrow(issue, outcome, owner, exception);
            return null;
        }

        owner?.Dispose();
        return exception;
    }
}
