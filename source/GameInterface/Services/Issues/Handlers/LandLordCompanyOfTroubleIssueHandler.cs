using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative Company of Trouble issue replication - creation-time broadcast (no captured field
/// needed, see <see cref="ILandLordCompanyOfTroubleIssueInterface"/>'s type doc comment) and the trigger-time
/// enemy-party spawn-gate request/broadcast routing (see
/// <see cref="Patches.LandLordCompanyOfTroubleEnemyPartySpawnGatePatch"/>'s doc comment) - which also folds this
/// quest's "battle-start-approval" (task item 4) into the same round trip, since there is no separately-callable
/// battle-start method here to gate independently.
///
/// Deliberately does NOT subscribe to any accept-quest/finalize message of its own - this issue type rides
/// <see cref="Patches.IssueAcceptancePatches"/>/<see cref="Patches.IssueFinalizedPatches"/> entirely unchanged
/// (see <see cref="GenericAcceptMirrorIssueTypes"/>'s LandLordCompanyOfTrouble entry and
/// <see cref="ILandLordCompanyOfTroubleIssueInterface"/>'s type doc comment for why nothing here needs its own
/// bespoke accept/finalize capture). Also deliberately does NOT register in
/// <see cref="GenericAcceptMirrorIssueTypes.AlternativeSolutionMirrorEligible"/> -
/// <c>LandLordCompanyOfTroubleIssue.IsThereAlternativeSolution</c> is <c>false</c> (confirmed by decompile), so
/// this quest has no alternative-solution path at all.
/// </summary>
internal class LandLordCompanyOfTroubleIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LandLordCompanyOfTroubleIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ILandLordCompanyOfTroubleIssueInterface issueInterface;

    public LandLordCompanyOfTroubleIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ILandLordCompanyOfTroubleIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<LandLordCompanyOfTroubleIssueCreated>(Handle_LandLordCompanyOfTroubleIssueCreated);
        messageBroker.Subscribe<NetworkLandLordCompanyOfTroubleIssueCreated>(Handle_NetworkLandLordCompanyOfTroubleIssueCreated);

        messageBroker.Subscribe<LandLordCompanyOfTroubleEnemyPartySpawnRequested>(Handle_EnemyPartySpawnRequested);
        messageBroker.Subscribe<NetworkLandLordCompanyOfTroubleEnemyPartySpawnRequest>(Handle_NetworkEnemyPartySpawnRequest);
        messageBroker.Subscribe<LandLordCompanyOfTroubleEnemyPartySpawned>(Handle_EnemyPartySpawned);
        messageBroker.Subscribe<NetworkLandLordCompanyOfTroubleEnemyPartySpawned>(Handle_NetworkEnemyPartySpawned);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LandLordCompanyOfTroubleIssueCreated>(Handle_LandLordCompanyOfTroubleIssueCreated);
        messageBroker.Unsubscribe<NetworkLandLordCompanyOfTroubleIssueCreated>(Handle_NetworkLandLordCompanyOfTroubleIssueCreated);

        messageBroker.Unsubscribe<LandLordCompanyOfTroubleEnemyPartySpawnRequested>(Handle_EnemyPartySpawnRequested);
        messageBroker.Unsubscribe<NetworkLandLordCompanyOfTroubleEnemyPartySpawnRequest>(Handle_NetworkEnemyPartySpawnRequest);
        messageBroker.Unsubscribe<LandLordCompanyOfTroubleEnemyPartySpawned>(Handle_EnemyPartySpawned);
        messageBroker.Unsubscribe<NetworkLandLordCompanyOfTroubleEnemyPartySpawned>(Handle_NetworkEnemyPartySpawned);
    }

    // --- Creation: server rolls once, broadcasts so every client independently, deterministically mirrors it ---

    private void Handle_LandLordCompanyOfTroubleIssueCreated(MessagePayload<LandLordCompanyOfTroubleIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        network.SendAll(new NetworkLandLordCompanyOfTroubleIssueCreated(ownerId));
    }

    private void Handle_NetworkLandLordCompanyOfTroubleIssueCreated(MessagePayload<NetworkLandLordCompanyOfTroubleIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            // Idempotent: a resend (or the full save transfer at join already having restored this via the
            // normal savegame) must not create a second issue for the same hero.
            if (owner.Issue != null) return;

            var replicated = issueInterface.ConstructReplicated(owner);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }

    // --- Trigger-time enemy-party spawn gate: see Patches.LandLordCompanyOfTroubleEnemyPartySpawnGatePatch's
    //     doc comment. Also carries the folded-in battle-start-approval (task item 4) - see its own doc comment.

    private void Handle_EnemyPartySpawnRequested(MessagePayload<LandLordCompanyOfTroubleEnemyPartySpawnRequested> payload)
    {
        var data = payload.What;
        if (data.Owner == null || !objectManager.TryGetIdWithLogging(data.Owner, out var ownerId)) return;

        // Only ever published on a client (see the Patch's Prefix) - a client's own SendAll only ever reaches
        // the server.
        network.SendAll(new NetworkLandLordCompanyOfTroubleEnemyPartySpawnRequest(ownerId, data.Position, data.TroopCount));
    }

    private void Handle_NetworkEnemyPartySpawnRequest(MessagePayload<NetworkLandLordCompanyOfTroubleEnemyPartySpawnRequest> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            if (owner.Issue?.IssueQuest is not LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest)
            {
                Logger.Error("No LandLordCompanyOfTroubleIssueQuest mirror available yet for owner {Owner}", data.OwnerId);
                return;
            }

            // Idempotent (CreateCompanyEnemyPartyOnServer itself already no-ops if the party exists) - a resend
            // must not spawn (or re-broadcast) a second enemy party for the same quest.
            var party = issueInterface.CreateCompanyEnemyPartyOnServer(owner, data.Position, data.TroopCount);
            if (party == null)
            {
                Logger.Error("Failed to create the replicated Company of Trouble enemy party for owner {Owner}", data.OwnerId);
                return;
            }

            messageBroker.Publish(this, new LandLordCompanyOfTroubleEnemyPartySpawned(owner, party));
        });
    }

    private void Handle_EnemyPartySpawned(MessagePayload<LandLordCompanyOfTroubleEnemyPartySpawned> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        if (data.Owner == null || data.CompanyOfTroubleParty == null) return;
        if (!objectManager.TryGetIdWithLogging(data.Owner, out var ownerId)) return;
        if (!objectManager.TryGetIdWithLogging(data.CompanyOfTroubleParty, out var partyId)) return;

        network.SendAll(new NetworkLandLordCompanyOfTroubleEnemyPartySpawned(ownerId, partyId));
    }

    private void Handle_NetworkEnemyPartySpawned(MessagePayload<NetworkLandLordCompanyOfTroubleEnemyPartySpawned> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.CompanyOfTroublePartyId, out var party)) return;

            // Force-writes _companyOfTroubleParty AND flips _battleWillStart on EVERY peer's own mirror
            // (including the true owner-client, whose own local company_of_trouble_menu_on_init next-tick read
            // of _battleWillStart is what actually launches the scripted battle - see the type doc comment for
            // why this satisfies task item 4 without a second round trip). Harmless parity bookkeeping on every
            // bystander peer, matching every other quest in this family's "parity only, not load-bearing"
            // precedent for fields only ever meaningfully read on the true owner's own machine.
            issueInterface.ForceCompanyEnemyParty(owner, party);
        });
    }
}
