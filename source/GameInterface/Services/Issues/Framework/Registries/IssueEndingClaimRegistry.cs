using Common.Messaging;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Registries;

internal class IssueEndingClaimRegistry : IIssueEndingClaimRegistry, IDisposable
{
    private readonly IMessageBroker messageBroker;
    // One claim per issue whose ending started, collected with the issue
    private readonly ConditionalWeakTable<IssueBase, IssueEndingClaim> claims = new();

    public IssueEndingClaimRegistry(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<AlternativeSolutionEndingObserved>(RememberSentEnding);
    }

    public void Dispose() => messageBroker.Unsubscribe<AlternativeSolutionEndingObserved>(RememberSentEnding);

    public bool TryClaim(IssueBase issue)
    {
        if (claims.TryGetValue(issue, out _)) return false;

        claims.Add(issue, new IssueEndingClaim());
        return true;
    }

    public IssueEndingClaim GetOrClaim(IssueBase issue) => claims.GetValue(issue, _ => new IssueEndingClaim());

    public void NoteTroopReturn(IssueBase issue)
    {
        if (claims.TryGetValue(issue, out var claim)) claim.ReturnStarted = true;
    }

    private void RememberSentEnding(MessagePayload<AlternativeSolutionEndingObserved> payload)
    {
        if (claims.TryGetValue(payload.What.Issue, out var claim)) claim.SentEnding ??= payload.What.Ending;
    }
}
