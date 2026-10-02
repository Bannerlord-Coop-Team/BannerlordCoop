using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Handlers;

using Issue = CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue;

internal sealed class CapturedByBountyHuntersIssueHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IIssueGenerationRegistry generations;

    public CapturedByBountyHuntersIssueHandler(
        IMessageBroker messageBroker, IObjectManager objectManager, INetwork network, IIssueGenerationRegistry generations)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.generations = generations;
        messageBroker.Subscribe<CapturedByBountyHuntersIssueCreated>(HandleCreated);
        messageBroker.Subscribe<NetworkCapturedByBountyHuntersIssueCreated>(HandleNetworkCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CapturedByBountyHuntersIssueCreated>(HandleCreated);
        messageBroker.Unsubscribe<NetworkCapturedByBountyHuntersIssueCreated>(HandleNetworkCreated);
    }

    private void HandleCreated(MessagePayload<CapturedByBountyHuntersIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;
        if (!objectManager.TryGetIdWithLogging(issue._hideout, out var hideoutId)) return;
        var generation = generations.Bump(issue.IssueOwner);
        network.SendAll(new NetworkCapturedByBountyHuntersIssueCreated(
            ownerId, hideoutId, generation, issue.IssueDifficultyMultiplier, issue.StringId, issue.IssueDueTime));
    }

    private void HandleNetworkCreated(MessagePayload<NetworkCapturedByBountyHuntersIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (generations.TryGetGeneration(owner, out var current) && current >= data.Generation) return;
            if (owner.Issue != null) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.HideoutId, out var hideout)) return;

            using (new AllowedThread())
            {
                PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero giver) =>
                {
                    var issue = new Issue(giver, hideout);
                    issue._issueDifficultyMultiplier = data.Difficulty;
                    return issue;
                };
                var potentialIssue = new PotentialIssueData(factory, typeof(Issue), IssueBase.IssueFrequency.Common);
                if (Campaign.Current.IssueManager.CreateNewIssue(in potentialIssue, owner))
                {
                    owner.Issue.StringId = data.IssueId;
                    owner.Issue.IssueDueTime = data.DueTime;
                    generations.SetGeneration(owner, data.Generation);
                }
            }
        });
    }
}
