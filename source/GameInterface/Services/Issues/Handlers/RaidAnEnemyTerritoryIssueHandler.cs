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
/// Server-authoritative Raid an Enemy Territory issue replication - creation-time enemy-kingdom capture (same
/// shape as <see cref="TheConquestOfSettlementIssueHandler"/>'s creation half) plus the raid-progress broadcast
/// <see cref="Patches.RaidAnEnemyTerritoryRaidCompletionPatches"/>'s bug fix needs (see that type's doc comment
/// for why this quest type - unlike Conquest - needs its own explicit mid-quest progress sync).
///
/// Deliberately does NOT subscribe to any accept-quest/finalize message of its own - this issue type rides
/// <see cref="Patches.IssueAcceptancePatches"/>/<see cref="Patches.IssueFinalizedPatches"/> entirely unchanged
/// (see <see cref="Interfaces.GenericAcceptMirrorIssueTypes"/>'s entry and
/// <see cref="Interfaces.IRaidAnEnemyTerritoryIssueInterface"/>'s type doc comment for why nothing here needs
/// its own bespoke accept/finalize capture). It has no alternative-solution path
/// (<c>IsThereAlternativeSolution == false</c>, confirmed by decompile), so no
/// <c>NewIssueTypesAlternativeSolutionPatches</c> registration is needed either.
/// </summary>
internal class RaidAnEnemyTerritoryIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<RaidAnEnemyTerritoryIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IRaidAnEnemyTerritoryIssueInterface issueInterface;

    public RaidAnEnemyTerritoryIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IRaidAnEnemyTerritoryIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<RaidAnEnemyTerritoryIssueCreated>(Handle_RaidAnEnemyTerritoryIssueCreated);
        messageBroker.Subscribe<NetworkRaidAnEnemyTerritoryIssueCreated>(Handle_NetworkRaidAnEnemyTerritoryIssueCreated);
        messageBroker.Subscribe<RaidAnEnemyTerritoryVillageRaided>(Handle_RaidAnEnemyTerritoryVillageRaided);
        messageBroker.Subscribe<NetworkRaidAnEnemyTerritoryVillageRaided>(Handle_NetworkRaidAnEnemyTerritoryVillageRaided);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RaidAnEnemyTerritoryIssueCreated>(Handle_RaidAnEnemyTerritoryIssueCreated);
        messageBroker.Unsubscribe<NetworkRaidAnEnemyTerritoryIssueCreated>(Handle_NetworkRaidAnEnemyTerritoryIssueCreated);
        messageBroker.Unsubscribe<RaidAnEnemyTerritoryVillageRaided>(Handle_RaidAnEnemyTerritoryVillageRaided);
        messageBroker.Unsubscribe<NetworkRaidAnEnemyTerritoryVillageRaided>(Handle_NetworkRaidAnEnemyTerritoryVillageRaided);
    }

    // --- Creation: server rolls once, broadcasts the resolved enemy kingdom ---

    private void Handle_RaidAnEnemyTerritoryIssueCreated(MessagePayload<RaidAnEnemyTerritoryIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureEnemyKingdom(issue, out var enemyKingdom))
        {
            Logger.Error("Could not capture Raid an Enemy Territory issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(enemyKingdom, out var enemyKingdomId)) return;

        network.SendAll(new NetworkRaidAnEnemyTerritoryIssueCreated(ownerId, enemyKingdomId));
    }

    private void Handle_NetworkRaidAnEnemyTerritoryIssueCreated(MessagePayload<NetworkRaidAnEnemyTerritoryIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            // Idempotent: a resend (or the full save transfer at join already having restored this via the
            // normal savegame) must not create a second issue for the same hero.
            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<Kingdom>(data.EnemyKingdomId, out var enemyKingdom)) return;

            var replicated = issueInterface.ConstructReplicated(owner, enemyKingdom);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }

    // --- Raid progress: server resolves+applies locally, broadcasts to every other peer ---

    private void Handle_RaidAnEnemyTerritoryVillageRaided(MessagePayload<RaidAnEnemyTerritoryVillageRaided> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        if (!objectManager.TryGetIdWithLogging(data.QuestGiver, out var questGiverId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Settlement, out var settlementId)) return;

        network.SendAll(new NetworkRaidAnEnemyTerritoryVillageRaided(questGiverId, settlementId));
    }

    private void Handle_NetworkRaidAnEnemyTerritoryVillageRaided(MessagePayload<NetworkRaidAnEnemyTerritoryVillageRaided> payload)
    {
        // The server already applied this to its own local mirror directly (see
        // RaidAnEnemyTerritoryRaidCompletionPatches.Resolve) before publishing the local event this broadcast
        // came from - only every OTHER peer needs to apply it here.
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.QuestGiverId, out var questGiver)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement)) return;

            issueInterface.TryApplyRaidedVillage(questGiver, settlement);
        });
    }
}
