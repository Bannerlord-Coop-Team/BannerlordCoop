using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Handlers;

internal class VassalServiceHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<VassalServiceHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IKingdomMembershipState kingdomMembershipState;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;

    public VassalServiceHandler(
        IMessageBroker messageBroker,
        IKingdomMembershipState kingdomMembershipState,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.kingdomMembershipState = kingdomMembershipState;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;

        messageBroker.Subscribe<VassalServiceAccepted>(HandleVassalServiceAccepted);
        messageBroker.Subscribe<RequestVassalService>(HandleRequestVassalService);
        messageBroker.Subscribe<VassalServiceResult>(HandleVassalServiceResult);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VassalServiceAccepted>(HandleVassalServiceAccepted);
        messageBroker.Unsubscribe<RequestVassalService>(HandleRequestVassalService);
        messageBroker.Unsubscribe<VassalServiceResult>(HandleVassalServiceResult);
    }

    private void HandleVassalServiceAccepted(MessagePayload<VassalServiceAccepted> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.Kingdom, out var kingdomId)) return;

        network.SendAll(new RequestVassalService(kingdomId, payload.What.GrantRewards));
    }

    private void HandleRequestVassalService(MessagePayload<RequestVassalService> payload)
    {
        if (ModInformation.IsClient) return;

        if (!(payload.Who is NetPeer peer) || !playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Error("Received {Message} without a registered player peer", nameof(RequestVassalService));
            return;
        }

        GameThread.RunSafe(() =>
        {
            bool accepted = ApplyVassalage(
                player.ClanId,
                player.HeroId,
                payload.What.KingdomId,
                payload.What.GrantRewards);

            network.Send(peer, new VassalServiceResult(
                payload.What.KingdomId,
                accepted,
                accepted && payload.What.GrantRewards));
        }, context: nameof(VassalServiceHandler));
    }

    private bool ApplyVassalage(string clanId, string heroId, string kingdomId, bool grantRewards)
    {
        if (!objectManager.TryGetObjectWithLogging<Clan>(clanId, out var clan)) return false;
        if (!objectManager.TryGetObjectWithLogging<Hero>(heroId, out var hero)) return false;
        if (!objectManager.TryGetObjectWithLogging<Kingdom>(kingdomId, out var kingdom)) return false;

        if (hero.Clan != clan || clan.Tier < Campaign.Current.Models.ClanTierModel.VassalEligibleTier)
        {
            Logger.Warning("Rejected vassal service request for clan {ClanId} because it is no longer eligible", clanId);
            return false;
        }

        Kingdom previousKingdom = clan.Kingdom;

        if (clan.Kingdom == kingdom)
        {
            if (!clan.IsUnderMercenaryService)
            {
                Logger.Warning("Rejected vassal service request because clan {ClanId} already belongs to kingdom {KingdomId}", clanId, kingdomId);
                return false;
            }

            EndMercenaryServiceAction.EndByBecomingVassal(clan);
        }
        else
        {
            if (clan.Kingdom != null)
            {
                if (!clan.IsUnderMercenaryService)
                {
                    Logger.Warning("Rejected vassal service request because clan {ClanId} already belongs to another kingdom", clanId);
                    return false;
                }

                EndMercenaryServiceAction.EndByLeavingKingdom(clan);
            }

            ChangeKingdomAction.ApplyByJoinToKingdom(clan, kingdom);
        }

        if (clan.Kingdom != kingdom || clan.IsUnderMercenaryService)
        {
            Logger.Error("Vassal service did not place clan {ClanId} in kingdom {KingdomId}", clanId, kingdomId);
            return false;
        }

        kingdomMembershipState.MoveClanToKingdom(
            previousKingdom,
            kingdom,
            clan,
            publishCollectionChanges: true,
            republishExistingCollections: true);
        if (!kingdom.Clans.Contains(clan))
        {
            Logger.Error("Vassal service did not add clan {ClanId} to kingdom {KingdomId} collections", clanId, kingdomId);
            return false;
        }

        var rewardsModel = Campaign.Current.Models.VassalRewardsModel;
        if (grantRewards && kingdom.Leader != null)
        {
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                hero,
                kingdom.Leader,
                rewardsModel.RelationRewardWithLeader);
        }

        GainKingdomInfluenceAction.ApplyForJoiningFaction(hero, rewardsModel.InfluenceReward);
        return true;
    }

    private void HandleVassalServiceResult(MessagePayload<VassalServiceResult> payload)
    {
        if (ModInformation.IsServer) return;

        GameThread.RunSafe(() =>
        {
            if (!payload.What.Accepted)
            {
                MBInformationManager.AddQuickInformation(
                    new TextObject("{=coop_vassalage_rejected}The vassalage agreement could not be completed."));
                return;
            }

            if (!payload.What.GrantRewards) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(payload.What.KingdomId, out var kingdom)) return;

            var behavior = Campaign.Current.GetCampaignBehavior<LordConversationsCampaignBehavior>();
            if (behavior == null || behavior._receivedVassalRewards) return;

            var rewardsModel = Campaign.Current.Models.VassalRewardsModel;
            InventoryScreenHelper.OpenScreenAsReceiveItems(
                rewardsModel.GetEquipmentRewardsForJoiningKingdom(kingdom),
                new TextObject("{=exbSCGzi}Reward Items"));
            PartyScreenHelper.OpenScreenAsReceiveTroops(
                rewardsModel.GetTroopRewardsForJoiningKingdom(kingdom),
                new TextObject("{=tKW8m6bZ}Reward Troops"));
            behavior._receivedVassalRewards = true;
        }, context: nameof(VassalServiceHandler));
    }
}
