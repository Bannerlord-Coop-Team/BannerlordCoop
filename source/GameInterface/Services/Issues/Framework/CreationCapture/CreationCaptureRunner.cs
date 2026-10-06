using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.CreationCapture;

/// <summary>
/// The server reads the values an issue construction produced and sends them. Clients never
/// create an issue on their own, they build it due to randomness being in issue constructors.
/// </summary>
internal class CreationCaptureRunner : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CreationCaptureRunner>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IQuestTypeRegistry registry;

    public CreationCaptureRunner(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IQuestTypeRegistry registry)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.registry = registry;

        messageBroker.Subscribe<IssueCreated>(Handle_IssueCreated);
        messageBroker.Subscribe<NetworkIssueCreated>(Handle_NetworkIssueCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<IssueCreated>(Handle_IssueCreated);
        messageBroker.Unsubscribe<NetworkIssueCreated>(Handle_NetworkIssueCreated);
    }

    private void Handle_IssueCreated(MessagePayload<IssueCreated> payload)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        var issue = payload.What.Issue;

        if (!registry.TryGet(issue.GetType(), out var descriptor))
        {
            return;
        }

        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId))
        {
            return;
        }

        var captured = descriptor.CreationCaptureStrategy.Capture(issue);

        network.SendAll(new NetworkIssueCreated(issueOwnerId, issue.GetType().Name, issue.StringId, captured));
    }

    private void Handle_NetworkIssueCreated(MessagePayload<NetworkIssueCreated> payload)
    {
        if (ModInformation.IsServer)
        {
            return;
        }

        var data = payload.What;
        
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.IssueOwnerId, out var issueOwner))
            {
                return;
            }

            if (!registry.TryGetByName(data.IssueTypeName, out var descriptor))
            {
                Logger.Error("Received an issue of unregistered type {type}", data.IssueTypeName);
                return;
            }

            if (issueOwner.Issue != null)
            {
                Logger.Warning("{hero} already has an issue, ignoring created {type}", data.IssueOwnerId, data.IssueTypeName);
                return;
            }

            var strategy = descriptor.CreationCaptureStrategy;
            var captured = data.Captured;

            var potentialIssue = new PotentialIssueData(
                (in PotentialIssueData _, Hero owner) => strategy.CreateIssue(owner, captured),
                descriptor.IssueType,
                IssueBase.IssueFrequency.Common);

            using (new AllowedThread())
            {
                Campaign.Current.IssueManager.CreateNewIssue(in potentialIssue, issueOwner);
            }

            var issue = issueOwner.Issue;
            if (issue == null)
            {
                return;
            }

            issue.StringId = data.IssueId;
            strategy.ApplyAfterCreation(issue, captured);
        }, context: nameof(CreationCaptureRunner));
    }
}
