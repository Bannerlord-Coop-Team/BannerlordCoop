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
/// Server-authoritative The Conquest of a Settlement issue replication - creation-time target-settlement
/// capture (same shape as <see cref="CaravanAmbushIssueHandler"/>'s creation half).
///
/// Deliberately does NOT subscribe to any accept-quest/finalize message of its own - this issue type rides
/// <see cref="Patches.IssueAcceptancePatches"/>/<see cref="Patches.IssueFinalizedPatches"/> entirely unchanged
/// (see <see cref="Interfaces.GenericAcceptMirrorIssueTypes"/>'s entry and
/// <see cref="Interfaces.ITheConquestOfSettlementIssueInterface"/>'s type doc comment for why nothing here
/// needs its own bespoke accept/finalize capture). It has no alternative-solution path
/// (<c>IsThereAlternativeSolution == false</c>, confirmed by decompile), so no
/// <c>NewIssueTypesAlternativeSolutionPatches</c> registration is needed either.
///
/// Bug 1 (siege completion) and Bug 2 (settlement-owner-changed by barter) are fixed entirely inside
/// <see cref="Patches.TheConquestOfSettlementSiegeCompletionPatches"/>/
/// <see cref="Patches.TheConquestOfSettlementSettlementOwnerChangedPatch"/> - neither needs a new network
/// message, since the identity they need (the real accepting player) is already resolvable server-side (or on
/// whichever peer the listener runs) from the SAME <see cref="VillageNeedsToolsIssueOwnership"/> registry the
/// existing generic accept-mirror flow already populates.
/// </summary>
internal class TheConquestOfSettlementIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TheConquestOfSettlementIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITheConquestOfSettlementIssueInterface issueInterface;

    public TheConquestOfSettlementIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ITheConquestOfSettlementIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<TheConquestOfSettlementIssueCreated>(Handle_TheConquestOfSettlementIssueCreated);
        messageBroker.Subscribe<NetworkTheConquestOfSettlementIssueCreated>(Handle_NetworkTheConquestOfSettlementIssueCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TheConquestOfSettlementIssueCreated>(Handle_TheConquestOfSettlementIssueCreated);
        messageBroker.Unsubscribe<NetworkTheConquestOfSettlementIssueCreated>(Handle_NetworkTheConquestOfSettlementIssueCreated);
    }

    // --- Creation: server rolls once, broadcasts the resolved target settlement ---

    private void Handle_TheConquestOfSettlementIssueCreated(MessagePayload<TheConquestOfSettlementIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureTargetSettlement(issue, out var targetSettlement))
        {
            Logger.Error("Could not capture The Conquest of a Settlement issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(targetSettlement, out var targetSettlementId)) return;

        network.SendAll(new NetworkTheConquestOfSettlementIssueCreated(ownerId, targetSettlementId));
    }

    private void Handle_NetworkTheConquestOfSettlementIssueCreated(MessagePayload<NetworkTheConquestOfSettlementIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            // Idempotent: a resend (or the full save transfer at join already having restored this via the
            // normal savegame) must not create a second issue for the same hero.
            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.TargetSettlementId, out var targetSettlement)) return;

            var replicated = issueInterface.ConstructReplicated(owner, targetSettlement);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }
}
