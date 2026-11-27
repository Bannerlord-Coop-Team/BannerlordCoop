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
    // One claim per issue whose troop ending started, collected with the issue
    private readonly ConditionalWeakTable<IssueBase, IssueEndingClaim> claims = new();

    public IssueEndingClaimRegistry(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<AlternativeSolutionEndingObserved>(RememberSentEnding);
    }

    public void Dispose() => messageBroker.Unsubscribe<AlternativeSolutionEndingObserved>(RememberSentEnding);

    public IssueEndingClaim Get(IssueBase issue) => claims.GetValue(issue, _ => new IssueEndingClaim());

    public void NoteTroopReturn(IssueBase issue) => Get(issue).ReturnStarted = true;

    private void RememberSentEnding(MessagePayload<AlternativeSolutionEndingObserved> payload) =>
        Get(payload.What.Issue).SentEnding ??= payload.What.Ending;
}
