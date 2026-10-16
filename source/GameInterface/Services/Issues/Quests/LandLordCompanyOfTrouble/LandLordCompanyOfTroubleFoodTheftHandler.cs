using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble;

internal class LandLordCompanyOfTroubleFoodTheftHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LandLordCompanyOfTroubleFoodTheftHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IIssueResolver issueResolver;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager playerManager;

    public LandLordCompanyOfTroubleFoodTheftHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IIssueResolver issueResolver,
        IIssueOwnershipRegistry ownership,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.issueResolver = issueResolver;
        this.ownership = ownership;
        this.playerManager = playerManager;

        messageBroker.Subscribe<LandLordCompanyOfTroubleFoodStolen>(Handle_FoodStolen);
        messageBroker.Subscribe<RequestLandLordCompanyOfTroubleFoodTheft>(Handle_RequestFoodTheft);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LandLordCompanyOfTroubleFoodStolen>(Handle_FoodStolen);
        messageBroker.Unsubscribe<RequestLandLordCompanyOfTroubleFoodTheft>(Handle_RequestFoodTheft);
    }

    private void Handle_FoodStolen(MessagePayload<LandLordCompanyOfTroubleFoodStolen> payload)
    {
        if (ModInformation.IsServer)
        {
            return;
        }

        var issue = IssueManager.GetIssueOfQuest(payload.What.Quest);
        if (issue == null ||
            !objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId) ||
            !objectManager.TryGetIdWithLogging(payload.What.Item, out var itemId))
        {
            return;
        }

        network.SendAll(new RequestLandLordCompanyOfTroubleFoodTheft(issueOwnerId, issue.StringId, itemId));
    }

    private void Handle_RequestFoodTheft(MessagePayload<RequestLandLordCompanyOfTroubleFoodTheft> payload)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        if (!(payload.Who is NetPeer peer) || !playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Error("Received {message} without a registered player peer", nameof(RequestLandLordCompanyOfTroubleFoodTheft));
            return;
        }

        var data = payload.What;

        GameThread.RunSafe(() => StealOnServer(player, data), context: nameof(LandLordCompanyOfTroubleFoodTheftHandler));
    }

    private void StealOnServer(Player player, RequestLandLordCompanyOfTroubleFoodTheft data)
    {
        if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) || !(issue.IssueQuest is Quest))
        {
            return;
        }

        if (!ownership.TryGetOwner(data.IssueOwnerId, data.IssueId, out var ownerControllerId) || ownerControllerId != player.ControllerId)
        {
            Logger.Warning("{controller} does not own {issue}", player.ControllerId, data.IssueId);
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var playerHero) ||
            !objectManager.TryGetObjectWithLogging<ItemObject>(data.ItemId, out var item))
        {
            return;
        }

        var roster = playerHero.PartyBelongedTo.ItemRoster;
        if (roster.GetItemNumber(item) <= 0)
        {
            return;
        }

        roster.AddToCounts(item, -1);
    }
}
