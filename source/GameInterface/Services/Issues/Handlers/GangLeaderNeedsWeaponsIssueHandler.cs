using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative Gang Leader Needs Weapons issue replication - creation-time broadcast (no captured
/// field needed, see <see cref="IGangLeaderNeedsWeaponsIssueInterface"/>'s type doc comment), the
/// guards-party spawn-gate request/broadcast routing (see
/// <see cref="Patches.GangLeaderNeedsWeaponsGuardsPartySpawnGatePatch"/>'s doc comment), and the battle-start
/// approval request/broadcast routing (see
/// <see cref="Patches.GangLeaderNeedsWeaponsBattleStartApprovalPatches"/>'s doc comment).
///
/// Deliberately does NOT subscribe to any accept-quest/alternative-solution-accept/finalize message of its own -
/// this issue type rides <see cref="Patches.IssueAcceptancePatches"/>/
/// <see cref="Patches.NewIssueTypesAlternativeSolutionOwnershipGatePatch"/>/<see cref="Patches.IssueFinalizedPatches"/>
/// entirely unchanged (see <see cref="GenericAcceptMirrorIssueTypes"/>'s GangLeaderNeedsWeapons entries and
/// <see cref="IGangLeaderNeedsWeaponsIssueInterface"/>'s type doc comment for why nothing here needs its own
/// bespoke accept/finalize capture).
/// </summary>
internal class GangLeaderNeedsWeaponsIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<GangLeaderNeedsWeaponsIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IGangLeaderNeedsWeaponsIssueInterface issueInterface;

    public GangLeaderNeedsWeaponsIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IGangLeaderNeedsWeaponsIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<GangLeaderNeedsWeaponsIssueCreated>(Handle_GangLeaderNeedsWeaponsIssueCreated);
        messageBroker.Subscribe<NetworkGangLeaderNeedsWeaponsIssueCreated>(Handle_NetworkGangLeaderNeedsWeaponsIssueCreated);

        messageBroker.Subscribe<GangLeaderNeedsWeaponsGuardsPartySpawnRequested>(Handle_GuardsPartySpawnRequested);
        messageBroker.Subscribe<NetworkGangLeaderNeedsWeaponsGuardsPartySpawnRequest>(Handle_NetworkGuardsPartySpawnRequest);
        messageBroker.Subscribe<GangLeaderNeedsWeaponsGuardsPartySpawned>(Handle_GuardsPartySpawned);
        messageBroker.Subscribe<NetworkGangLeaderNeedsWeaponsGuardsPartySpawned>(Handle_NetworkGuardsPartySpawned);

        messageBroker.Subscribe<GangLeaderNeedsWeaponsBattleStartRequested>(Handle_BattleStartRequested);
        messageBroker.Subscribe<NetworkGangLeaderNeedsWeaponsBattleStartRequest>(Handle_NetworkBattleStartRequest);
        messageBroker.Subscribe<GangLeaderNeedsWeaponsBattleApproved>(Handle_BattleApproved);
        messageBroker.Subscribe<NetworkGangLeaderNeedsWeaponsBattleApproved>(Handle_NetworkBattleApproved);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GangLeaderNeedsWeaponsIssueCreated>(Handle_GangLeaderNeedsWeaponsIssueCreated);
        messageBroker.Unsubscribe<NetworkGangLeaderNeedsWeaponsIssueCreated>(Handle_NetworkGangLeaderNeedsWeaponsIssueCreated);

        messageBroker.Unsubscribe<GangLeaderNeedsWeaponsGuardsPartySpawnRequested>(Handle_GuardsPartySpawnRequested);
        messageBroker.Unsubscribe<NetworkGangLeaderNeedsWeaponsGuardsPartySpawnRequest>(Handle_NetworkGuardsPartySpawnRequest);
        messageBroker.Unsubscribe<GangLeaderNeedsWeaponsGuardsPartySpawned>(Handle_GuardsPartySpawned);
        messageBroker.Unsubscribe<NetworkGangLeaderNeedsWeaponsGuardsPartySpawned>(Handle_NetworkGuardsPartySpawned);

        messageBroker.Unsubscribe<GangLeaderNeedsWeaponsBattleStartRequested>(Handle_BattleStartRequested);
        messageBroker.Unsubscribe<NetworkGangLeaderNeedsWeaponsBattleStartRequest>(Handle_NetworkBattleStartRequest);
        messageBroker.Unsubscribe<GangLeaderNeedsWeaponsBattleApproved>(Handle_BattleApproved);
        messageBroker.Unsubscribe<NetworkGangLeaderNeedsWeaponsBattleApproved>(Handle_NetworkBattleApproved);
    }

    // --- Creation: server rolls once, broadcasts so every client independently, deterministically mirrors it ---

    private void Handle_GangLeaderNeedsWeaponsIssueCreated(MessagePayload<GangLeaderNeedsWeaponsIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        network.SendAll(new NetworkGangLeaderNeedsWeaponsIssueCreated(ownerId));
    }

    private void Handle_NetworkGangLeaderNeedsWeaponsIssueCreated(MessagePayload<NetworkGangLeaderNeedsWeaponsIssueCreated> payload)
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

    // --- Guards-party spawn gate: see Patches.GangLeaderNeedsWeaponsGuardsPartySpawnGatePatch's doc comment ---

    private void Handle_GuardsPartySpawnRequested(MessagePayload<GangLeaderNeedsWeaponsGuardsPartySpawnRequested> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        // Only ever published on a client (see the Patch's Prefix) - a client's own SendAll only ever reaches
        // the server.
        network.SendAll(new NetworkGangLeaderNeedsWeaponsGuardsPartySpawnRequest(ownerId));
    }

    private void Handle_NetworkGuardsPartySpawnRequest(MessagePayload<NetworkGangLeaderNeedsWeaponsGuardsPartySpawnRequest> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            if (owner.Issue?.IssueQuest is not GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest)
            {
                Logger.Error("No GangLeaderNeedsWeaponsIssueQuest mirror available yet for owner {Owner}", data.OwnerId);
                return;
            }

            // Idempotent (and broadcasts via the Postfix on CreateGuardsParty only when the real body genuinely
            // executes) - a resend must not spawn (or re-broadcast) a second guards party for the same quest.
            issueInterface.CreateGuardsPartyOnServer(owner);
        });
    }

    private void Handle_GuardsPartySpawned(MessagePayload<GangLeaderNeedsWeaponsGuardsPartySpawned> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        if (data.Owner == null || data.GuardsParty == null) return;
        if (!objectManager.TryGetIdWithLogging(data.Owner, out var ownerId)) return;
        if (!objectManager.TryGetIdWithLogging(data.GuardsParty, out var guardsPartyId)) return;

        network.SendAll(new NetworkGangLeaderNeedsWeaponsGuardsPartySpawned(ownerId, guardsPartyId));
    }

    private void Handle_NetworkGuardsPartySpawned(MessagePayload<NetworkGangLeaderNeedsWeaponsGuardsPartySpawned> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.GuardsPartyId, out var guardsParty)) return;

            issueInterface.ForceGuardsParty(owner, guardsParty);

            // Only the genuine owner's own machine should finish the "guards are waiting for you" side effect
            // the blocked OnSettlementEnter couldn't complete synchronously - see
            // Patches.GangLeaderNeedsWeaponsGuardsPartySpawnGatePatch's doc comment.
            if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(owner)) return;
            if (owner.Issue?.IssueQuest is not GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest quest) return;

            issueInterface.OpenGuardConversationIfPossible(quest);
        });
    }

    // --- Battle-start approval: see Patches.GangLeaderNeedsWeaponsBattleStartApprovalPatches's doc comment ---

    private void Handle_BattleStartRequested(MessagePayload<GangLeaderNeedsWeaponsBattleStartRequested> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        network.SendAll(new NetworkGangLeaderNeedsWeaponsBattleStartRequest(ownerId));
    }

    private void Handle_NetworkBattleStartRequest(MessagePayload<NetworkGangLeaderNeedsWeaponsBattleStartRequest> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            if (owner.Issue?.IssueQuest is not GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest)
            {
                Logger.Error("No GangLeaderNeedsWeaponsIssueQuest mirror available yet for owner {Owner}", data.OwnerId);
                return;
            }

            // Sanity check (same trust model as every other request-forward handler in this family - the
            // server trusts that only the client-side ownership-gated Prefix ever sends this): the guards party
            // must exist before a fight against it can be approved.
            if (!issueInterface.TryCaptureGuardsParty(owner, out _))
            {
                Logger.Error("Battle start requested for owner {Owner} before its guards party exists", data.OwnerId);
                return;
            }

            messageBroker.Publish(this, new GangLeaderNeedsWeaponsBattleApproved(owner));
        });
    }

    private void Handle_BattleApproved(MessagePayload<GangLeaderNeedsWeaponsBattleApproved> payload)
    {
        if (ModInformation.IsClient) return;

        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        network.SendAll(new NetworkGangLeaderNeedsWeaponsBattleApproved(ownerId));
    }

    private void Handle_NetworkBattleApproved(MessagePayload<NetworkGangLeaderNeedsWeaponsBattleApproved> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            if (VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(owner))
            {
                // The genuine owner's own machine (host or client, whichever this is) - actually launch the
                // mission, the one thing that can never be relocated to the server (see
                // IGangLeaderNeedsWeaponsIssueInterface's type doc comment).
                issueInterface.InvokeRealStartFight(owner);
            }
            else
            {
                // Parity-only bookkeeping for every other peer's mirror - see ForceCheckForBattleResult's doc
                // comment.
                issueInterface.ForceCheckForBattleResult(owner);
            }
        });
    }
}
