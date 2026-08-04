using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative creation replication for Rival Gang Moving In - captures + broadcasts the creation-time
/// <c>RivalGangLeader</c> pick (see <see cref="IRivalGangMovingInIssueInterface"/>'s doc comment) so every
/// client's mirror is built against the same <see cref="Hero"/> instead of independently re-walking
/// <c>Notables</c>.
///
/// Deliberately does NOT have its own accept-time ownership plumbing (unlike e.g.
/// <see cref="BettingFraudIssueHandler"/>'s 4-message shape): <c>RivalGangMovingInIssueQuest.GenerateIssueQuest</c>
/// forwards only already-frozen-by-creation-time fields (owner/RivalGangLeader/a fixed 8-day duration/RewardGold/
/// IssueDifficultyMultiplier - all deterministic, see the interface's doc comment), so once
/// <c>RivalGangMovingInIssueBehavior.RivalGangMovingInIssue</c> is registered in
/// <see cref="GenericAcceptMirrorIssueTypes.QuestSolutionMirrorEligible"/>/<c>AlternativeSolutionMirrorEligible</c>,
/// the EXISTING <see cref="VillageNeedsToolsIssueHandler"/> already handles the accept-time
/// <c>VillageIssueQuestAcceptTriggered</c>/<c>RequestVillageIssueAcceptQuest</c>/<c>NetworkVillageIssueQuestAccepted</c>
/// round trip (via <see cref="Patches.IssueAcceptancePatches"/>) generically for ANY type in that set, INCLUDING
/// populating <see cref="VillageNeedsToolsIssueOwnership"/> - see that handler's own doc comment. A bespoke
/// accept handler here would just be a parallel, unused duplicate of infrastructure that already runs. Party
/// spawn (the one genuinely bespoke mechanic this quest needs) is handled by
/// <see cref="RivalGangMovingInPartyCreationCoordinator"/> instead.
/// </summary>
internal class RivalGangMovingInIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<RivalGangMovingInIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IRivalGangMovingInIssueInterface issueInterface;

    public RivalGangMovingInIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IRivalGangMovingInIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<RivalGangMovingInIssueCreated>(Handle_RivalGangMovingInIssueCreated);
        messageBroker.Subscribe<NetworkRivalGangMovingInIssueCreated>(Handle_NetworkRivalGangMovingInIssueCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RivalGangMovingInIssueCreated>(Handle_RivalGangMovingInIssueCreated);
        messageBroker.Unsubscribe<NetworkRivalGangMovingInIssueCreated>(Handle_NetworkRivalGangMovingInIssueCreated);
    }

    private void Handle_RivalGangMovingInIssueCreated(MessagePayload<RivalGangMovingInIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        var rivalGangLeader = issue.RivalGangLeader;
        if (rivalGangLeader == null)
        {
            Logger.Error("Rival Gang Moving In issue for owner {Owner} has no RivalGangLeader; cannot replicate", ownerId);
            return;
        }
        if (!objectManager.TryGetIdWithLogging(rivalGangLeader, out var rivalGangLeaderId)) return;

        network.SendAll(new NetworkRivalGangMovingInIssueCreated(ownerId, rivalGangLeaderId));
    }

    private void Handle_NetworkRivalGangMovingInIssueCreated(MessagePayload<NetworkRivalGangMovingInIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            // Idempotent: a resend (or the full save transfer at join already having restored this via the
            // normal savegame, since IssueManager's own dictionary is a real SaveableField) must not create a
            // second issue for the same hero.
            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<Hero>(data.RivalGangLeaderId, out var rivalGangLeader)) return;

            var replicated = issueInterface.ConstructReplicated(owner, rivalGangLeader);
            issueInterface.RegisterReplicated(owner, replicated);
        });
    }
}
