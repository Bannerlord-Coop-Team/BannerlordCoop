using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative Scout Enemy Garrisons issue CREATION replication only - acceptance rides the fully
/// generic mirror (see <c>GenericAcceptMirrorIssueTypes</c>) unchanged, and removal rides the existing generic
/// finalize choke point (see <see cref="VillageNeedsCraftingMaterialsIssueHandler"/>'s doc comment for why
/// per-type handlers deliberately don't add their own parallel finalize subscription).
/// </summary>
internal class ScoutEnemyGarrisonsIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ScoutEnemyGarrisonsIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IScoutEnemyGarrisonsIssueInterface issueInterface;

    public ScoutEnemyGarrisonsIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IScoutEnemyGarrisonsIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<ScoutEnemyGarrisonsIssueCreated>(Handle_ScoutEnemyGarrisonsIssueCreated);
        messageBroker.Subscribe<NetworkScoutEnemyGarrisonsIssueCreated>(Handle_NetworkScoutEnemyGarrisonsIssueCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ScoutEnemyGarrisonsIssueCreated>(Handle_ScoutEnemyGarrisonsIssueCreated);
        messageBroker.Unsubscribe<NetworkScoutEnemyGarrisonsIssueCreated>(Handle_NetworkScoutEnemyGarrisonsIssueCreated);
    }

    private void Handle_ScoutEnemyGarrisonsIssueCreated(MessagePayload<ScoutEnemyGarrisonsIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var s1, out var s2, out var s3))
        {
            Logger.Error("Could not capture Scout Enemy Garrisons issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(s1, out var s1Id)) return;
        if (!objectManager.TryGetIdWithLogging(s2, out var s2Id)) return;
        if (!objectManager.TryGetIdWithLogging(s3, out var s3Id)) return;

        network.SendAll(new NetworkScoutEnemyGarrisonsIssueCreated(ownerId, s1Id, s2Id, s3Id));
    }

    private void Handle_NetworkScoutEnemyGarrisonsIssueCreated(MessagePayload<NetworkScoutEnemyGarrisonsIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (owner.Issue != null) return; // idempotent

            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.Settlement1Id, out var s1)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.Settlement2Id, out var s2)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.Settlement3Id, out var s3)) return;

            var replicated = issueInterface.ConstructReplicated(owner, s1, s2, s3);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }
}
