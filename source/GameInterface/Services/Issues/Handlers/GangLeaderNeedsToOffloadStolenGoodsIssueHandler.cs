using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative Gang Leader Needs to Offload Stolen Goods issue replication - mirrors
/// <see cref="VillageNeedsCraftingMaterialsIssueHandler"/>'s creation/acceptance routing shape closely (same
/// genuine-accept-runs-locally-first, server-arbitrates-races, never-trust-a-client-claimed-ControllerId
/// pattern), but with its own message types (see Messages/GangLeaderNeedsToOffloadStolenGoodsIssueMessages.cs's
/// file-level note) and its own accept-time price/reward force-write mechanism - see
/// <see cref="IGangLeaderNeedsToOffloadStolenGoodsIssueInterface"/>'s type doc comment for the central "why".
///
/// Deliberately does NOT subscribe to <c>NetworkVillageIssueAcceptRejected</c>, and sends it directly (rather
/// than a bespoke reject type) when arbitrating a lost race: <see cref="VillageNeedsToolsIssueHandler"/>'s own
/// handler for that message is already fully generic (<c>issueInterface.RejectAcceptance(owner)</c>, no type
/// check at all - see <see cref="Interfaces.VillageNeedsToolsIssueInterface.RejectAcceptance"/>'s doc comment),
/// so reusing it directly avoids ever needing a bespoke <c>RejectAcceptance</c> of this type's own to
/// (re)introduce the accept-race rollback bug already found and fixed three times via copy-paste - per the
/// task's explicit instruction.
///
/// Also does NOT subscribe to a finalize/removal message of its own, for the same reason
/// <see cref="VillageNeedsCraftingMaterialsIssueHandler"/>'s doc comment gives: <c>VillageIssueFinalizedTriggered</c>/
/// <c>RequestVillageIssueRemoved</c>/<c>NetworkVillageIssueRemoved</c> and <see cref="VillageNeedsToolsIssueHandler"/>'s
/// handlers for them are already fully generic (via <c>DisableAllIssueBehaviorsExceptAllowlist.IsAllowlisted</c>) -
/// once this type is allowlisted, that existing, unmodified handler already correctly tears down this issue
/// type on every peer.
/// </summary>
internal class GangLeaderNeedsToOffloadStolenGoodsIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<GangLeaderNeedsToOffloadStolenGoodsIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IGangLeaderNeedsToOffloadStolenGoodsIssueInterface issueInterface;
    private readonly IPlayerManager playerManager;

    public GangLeaderNeedsToOffloadStolenGoodsIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IGangLeaderNeedsToOffloadStolenGoodsIssueInterface issueInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<GangLeaderStolenGoodsIssueCreated>(Handle_GangLeaderStolenGoodsIssueCreated);
        messageBroker.Subscribe<NetworkGangLeaderStolenGoodsIssueCreated>(Handle_NetworkGangLeaderStolenGoodsIssueCreated);

        messageBroker.Subscribe<GangLeaderStolenGoodsQuestAcceptTriggered>(Handle_GangLeaderStolenGoodsQuestAcceptTriggered);
        messageBroker.Subscribe<RequestGangLeaderStolenGoodsAcceptQuest>(Handle_RequestGangLeaderStolenGoodsAcceptQuest);
        messageBroker.Subscribe<NetworkGangLeaderStolenGoodsQuestAccepted>(Handle_NetworkGangLeaderStolenGoodsQuestAccepted);

        messageBroker.Subscribe<GangLeaderStolenGoodsAlternativeAcceptTriggered>(Handle_GangLeaderStolenGoodsAlternativeAcceptTriggered);
        messageBroker.Subscribe<RequestGangLeaderStolenGoodsAcceptAlternative>(Handle_RequestGangLeaderStolenGoodsAcceptAlternative);
        messageBroker.Subscribe<NetworkGangLeaderStolenGoodsAlternativeAccepted>(Handle_NetworkGangLeaderStolenGoodsAlternativeAccepted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GangLeaderStolenGoodsIssueCreated>(Handle_GangLeaderStolenGoodsIssueCreated);
        messageBroker.Unsubscribe<NetworkGangLeaderStolenGoodsIssueCreated>(Handle_NetworkGangLeaderStolenGoodsIssueCreated);

        messageBroker.Unsubscribe<GangLeaderStolenGoodsQuestAcceptTriggered>(Handle_GangLeaderStolenGoodsQuestAcceptTriggered);
        messageBroker.Unsubscribe<RequestGangLeaderStolenGoodsAcceptQuest>(Handle_RequestGangLeaderStolenGoodsAcceptQuest);
        messageBroker.Unsubscribe<NetworkGangLeaderStolenGoodsQuestAccepted>(Handle_NetworkGangLeaderStolenGoodsQuestAccepted);

        messageBroker.Unsubscribe<GangLeaderStolenGoodsAlternativeAcceptTriggered>(Handle_GangLeaderStolenGoodsAlternativeAcceptTriggered);
        messageBroker.Unsubscribe<RequestGangLeaderStolenGoodsAcceptAlternative>(Handle_RequestGangLeaderStolenGoodsAcceptAlternative);
        messageBroker.Unsubscribe<NetworkGangLeaderStolenGoodsAlternativeAccepted>(Handle_NetworkGangLeaderStolenGoodsAlternativeAccepted);
    }

    // --- Creation: server rolls once, broadcasts the resolved terms ---

    private void Handle_GangLeaderStolenGoodsIssueCreated(MessagePayload<GangLeaderStolenGoodsIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var issueHideout, out var randomForStolenTradeGood, out var counterOfferHero))
        {
            Logger.Error("Could not capture Gang Leader Needs to Offload Stolen Goods issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(issueHideout, out var issueHideoutId)) return;
        if (!objectManager.TryGetIdWithLogging(counterOfferHero, out var counterOfferHeroId)) return;

        network.SendAll(new NetworkGangLeaderStolenGoodsIssueCreated(ownerId, issueHideoutId, randomForStolenTradeGood, counterOfferHeroId));
    }

    private void Handle_NetworkGangLeaderStolenGoodsIssueCreated(MessagePayload<NetworkGangLeaderStolenGoodsIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (owner.Issue != null) return; // idempotent

            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.IssueHideoutId, out var issueHideout)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.CounterOfferHeroId, out var counterOfferHero)) return;

            var replicated = issueInterface.ConstructReplicated(owner, issueHideout, data.RandomForStolenTradeGood);

            issueInterface.RegisterReplicated(owner, replicated, counterOfferHero);
        });
    }

    // --- Quest-solution acceptance: the accepting machine already applied it locally for real with its own
    // (not yet authoritative) price/reward terms; the server arbitrates a same-issue double-accept race and
    // resolves/broadcasts the authoritative terms every peer force-corrects to. ---

    private void Handle_GangLeaderStolenGoodsQuestAcceptTriggered(MessagePayload<GangLeaderStolenGoodsQuestAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            // The host's own live conversation just accepted - already authoritative, including the price/
            // reward terms it captured at the same instant.
            var hostControllerId = payload.What.ControllerId;
            VillageNeedsToolsIssueOwnership.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkGangLeaderStolenGoodsQuestAccepted(
                ownerId, hostControllerId, payload.What.StolenTradeGoodAmount, payload.What.StolenTradeGoodPrice,
                payload.What.RewardGold, payload.What.CounterOfferGold));
        }
        else
        {
            // A client's own live conversation already applied this locally with its own (not yet
            // authoritative) terms - tell the server so it can arbitrate a race and resolve the real terms
            // itself. Deliberately no price/reward/ControllerId fields here: none of this client's own
            // locally-rolled values can be trusted as authoritative.
            network.SendAll(new RequestGangLeaderStolenGoodsAcceptQuest(ownerId));
        }
    }

    private void Handle_RequestGangLeaderStolenGoodsAcceptQuest(MessagePayload<RequestGangLeaderStolenGoodsAcceptQuest> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerId = payload.What.OwnerId;
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;

            if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
            {
                Logger.Error("Rejecting {Message} from an unregistered/unknown requester for owner {Owner}",
                    nameof(RequestGangLeaderStolenGoodsAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkVillageIssueAcceptRejected(ownerId));
                return;
            }

            if (owner.Issue is GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue && owner.Issue.IsOngoingWithoutQuest)
            {
                // First valid request wins the race - replay on the server's own authoritative copy, capture
                // what that replay actually baked (the server's own market-price/IssueDifficultyMultiplier
                // rolls ARE the authoritative ones), then confirm those exact values to everyone.
                issueInterface.ReplayQuestAccepted(owner);
                if (!issueInterface.TryCaptureQuestFields(owner, out var stolenTradeGoodAmount, out var stolenTradeGoodPrice, out var rewardGold, out var counterOfferGold))
                {
                    Logger.Error("Replayed StartIssueQuest for owner {Owner} but could not read back its quest fields", ownerId);
                    return;
                }

                VillageNeedsToolsIssueOwnership.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkGangLeaderStolenGoodsQuestAccepted(
                    ownerId, player.ControllerId, stolenTradeGoodAmount, stolenTradeGoodPrice, rewardGold, counterOfferGold));
            }
            else
            {
                // Someone else already won the race (or the issue is gone) - reuse the shared, generic
                // rejection message/handler (see the type doc comment for why no bespoke one is needed).
                network.Send(requester, new NetworkVillageIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkGangLeaderStolenGoodsQuestAccepted(MessagePayload<NetworkGangLeaderStolenGoodsQuestAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            // Mirror (replaying if this peer has no quest yet) and force-correct the price/reward terms in the
            // same synchronous block that also records ownership - no TOCTOU window between "dialogue
            // registered" and "ownership known".
            issueInterface.MirrorQuestAccepted(owner, data.StolenTradeGoodAmount, data.StolenTradeGoodPrice, data.RewardGold, data.CounterOfferGold);
            VillageNeedsToolsIssueOwnership.SetOwner(owner, data.OwnerControllerId);
        });
    }

    // --- Alternative-solution acceptance: same shape, plus freezes the payout amounts (see the interface's
    // type doc comment for the task's second flagged gap). ---

    private void Handle_GangLeaderStolenGoodsAlternativeAcceptTriggered(MessagePayload<GangLeaderStolenGoodsAlternativeAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            VillageNeedsToolsIssueOwnership.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkGangLeaderStolenGoodsAlternativeAccepted(
                ownerId, hostControllerId, payload.What.StolenTradeGoodAmount, payload.What.RewardGold));
        }
        else
        {
            // Deliberately carries THIS client's own just-captured amount/reward - see
            // RequestGangLeaderStolenGoodsAcceptAlternative's own doc comment for why: unlike the quest-solution
            // path, the server has no replay to independently re-derive an authoritative value from, so it must
            // trust whichever peer's request wins the accept race.
            network.SendAll(new RequestGangLeaderStolenGoodsAcceptAlternative(ownerId, payload.What.StolenTradeGoodAmount, payload.What.RewardGold));
        }
    }

    private void Handle_RequestGangLeaderStolenGoodsAcceptAlternative(MessagePayload<RequestGangLeaderStolenGoodsAcceptAlternative> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
            {
                Logger.Error("Rejecting {Message} from an unregistered/unknown requester for owner {Owner}",
                    nameof(RequestGangLeaderStolenGoodsAcceptAlternative), data.OwnerId);
                if (requester != null) network.Send(requester, new NetworkVillageIssueAcceptRejected(data.OwnerId));
                return;
            }

            if (owner.Issue is GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue && owner.Issue.IsOngoingWithoutQuest)
            {
                // Trust the requester's own captured values (see the message type's doc comment for why -
                // no server-side replay exists for this path to independently re-derive an authoritative value
                // from; bare-capturing the server's OWN, never-accepted issue object here was a confirmed bug
                // found during this pass, since the server's own IssueDifficultyMultiplier/live item price at
                // this moment have nothing to do with what the genuine accepter actually baked).
                issueInterface.MirrorAlternativeAccepted(owner, data.StolenTradeGoodAmount, data.RewardGold);
                VillageNeedsToolsIssueOwnership.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkGangLeaderStolenGoodsAlternativeAccepted(data.OwnerId, player.ControllerId, data.StolenTradeGoodAmount, data.RewardGold));
            }
            else
            {
                network.Send(requester, new NetworkVillageIssueAcceptRejected(data.OwnerId));
            }
        });
    }

    private void Handle_NetworkGangLeaderStolenGoodsAlternativeAccepted(MessagePayload<NetworkGangLeaderStolenGoodsAlternativeAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            issueInterface.MirrorAlternativeAccepted(owner, data.StolenTradeGoodAmount, data.RewardGold);
            VillageNeedsToolsIssueOwnership.SetOwner(owner, data.OwnerControllerId);
        });
    }
}
