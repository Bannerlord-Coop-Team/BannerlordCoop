using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons.Messages;
using GameInterface.Services.ObjectManager;
using Issue = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons;

/// <summary>
/// Only the server rerolls an offered issue's targets, it sends the new targets so every client offers the same ones.
/// </summary>
internal class ScoutEnemyGarrisonsTargetsHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IIssueResolver issueResolver;
    private readonly ScoutEnemyGarrisonsQuestSolutionAcceptStrategy questSolutionAcceptStrategy;

    public ScoutEnemyGarrisonsTargetsHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IIssueResolver issueResolver,
        ScoutEnemyGarrisonsQuestSolutionAcceptStrategy questSolutionAcceptStrategy)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.issueResolver = issueResolver;
        this.questSolutionAcceptStrategy = questSolutionAcceptStrategy;

        messageBroker.Subscribe<ScoutEnemyGarrisonsTargetsChanged>(Handle_TargetsChanged);
        messageBroker.Subscribe<NetworkScoutEnemyGarrisonsTargetsChanged>(Handle_NetworkTargetsChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ScoutEnemyGarrisonsTargetsChanged>(Handle_TargetsChanged);
        messageBroker.Unsubscribe<NetworkScoutEnemyGarrisonsTargetsChanged>(Handle_NetworkTargetsChanged);
    }

    // Server only.
    private void Handle_TargetsChanged(MessagePayload<ScoutEnemyGarrisonsTargetsChanged> payload)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        var issue = payload.What.Issue;

        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId))
        {
            return;
        }

        network.SendAll(new NetworkScoutEnemyGarrisonsTargetsChanged(issueOwnerId, issue.StringId, questSolutionAcceptStrategy.Capture(issue)));
    }

    // Client only.
    private void Handle_NetworkTargetsChanged(MessagePayload<NetworkScoutEnemyGarrisonsTargetsChanged> payload)
    {
        if (ModInformation.IsServer)
        {
            return;
        }

        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !(issue is Issue scoutIssue) ||
                !issue.IsOngoingWithoutQuest)
            {
                return;
            }

            // A taken issue keeps the targets its accepter was given
            using (new AllowedThread())
            {
                questSolutionAcceptStrategy.Apply(scoutIssue, data.Targets);
            }
        }, context: nameof(ScoutEnemyGarrisonsTargetsHandler));
    }
}
