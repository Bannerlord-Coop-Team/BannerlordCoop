using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
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

internal class GangLeaderNeedsToOffloadStolenGoodsIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<GangLeaderNeedsToOffloadStolenGoodsIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IIssueGenerationRegistry generationRegistry;
    private readonly IIssueOwnershipRegistry ownershipRegistry;
    private readonly IPlayerManager playerManager;

    public GangLeaderNeedsToOffloadStolenGoodsIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IIssueGenerationRegistry generationRegistry,
        IIssueOwnershipRegistry ownershipRegistry,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.generationRegistry = generationRegistry;
        this.ownershipRegistry = ownershipRegistry;
        this.playerManager = playerManager;

        messageBroker.Subscribe<GangLeaderStolenGoodsIssueCreated>(Handle_GangLeaderStolenGoodsIssueCreated);
        messageBroker.Subscribe<NetworkGangLeaderStolenGoodsIssueCreated>(Handle_NetworkGangLeaderStolenGoodsIssueCreated);
        messageBroker.Subscribe<GangLeaderStolenGoodsStateSync>(Handle_GangLeaderStolenGoodsStateSync);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GangLeaderStolenGoodsIssueCreated>(Handle_GangLeaderStolenGoodsIssueCreated);
        messageBroker.Unsubscribe<NetworkGangLeaderStolenGoodsIssueCreated>(Handle_NetworkGangLeaderStolenGoodsIssueCreated);
        messageBroker.Unsubscribe<GangLeaderStolenGoodsStateSync>(Handle_GangLeaderStolenGoodsStateSync);
    }

    private void Handle_GangLeaderStolenGoodsIssueCreated(MessagePayload<GangLeaderStolenGoodsIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!GangLeaderNeedsToOffloadStolenGoodsQuestType.CreationCapture.TryCapture(issue, out var fields))
        {
            Logger.Error("Could not capture Gang Leader Needs to Offload Stolen Goods issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(fields.IssueHideout, out var issueHideoutId)) return;
        if (!objectManager.TryGetIdWithLogging(fields.CounterOfferHero, out var counterOfferHeroId)) return;

        var generation = generationRegistry.Bump(issue.IssueOwner);

        network.SendAll(new NetworkGangLeaderStolenGoodsIssueCreated(
            ownerId, issueHideoutId, fields.RandomForStolenTradeGood, counterOfferHeroId, generation));
    }

    private void Handle_NetworkGangLeaderStolenGoodsIssueCreated(MessagePayload<NetworkGangLeaderStolenGoodsIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            generationRegistry.SetGeneration(owner, data.Generation);

            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.IssueHideoutId, out var issueHideout)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.CounterOfferHeroId, out var counterOfferHero)) return;

            GangLeaderNeedsToOffloadStolenGoodsQuestType.ConstructAndRegisterReplicated(
                owner, issueHideout, data.RandomForStolenTradeGood, counterOfferHero);
        });
    }

    private void Handle_GangLeaderStolenGoodsStateSync(MessagePayload<GangLeaderStolenGoodsStateSync> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (requester == null || !playerManager.TryGetPlayer(requester, out var player)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (!ownershipRegistry.TryGetOwnerControllerId(owner, out var recordedOwner) || recordedOwner != player.ControllerId) return;
            if (owner.Issue?.IssueQuest is not GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest quest) return;

            quest._isPayingForGoods = data.IsPayingForGoods;
            quest._isFightingForGoods = data.IsFightingForGoods;
            quest._playerHasTheGoods = data.PlayerHasTheGoods;
        });
    }
}
