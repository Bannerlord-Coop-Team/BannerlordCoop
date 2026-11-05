using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Clans.Handlers;

/// <summary>
/// Applies accepted mercenary contracts on the server and lets existing synchronization publish the results.
/// </summary>
internal class MercenaryServiceHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MercenaryServiceHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly ICoopClanPermissions permissions;

    public MercenaryServiceHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        ICoopClanPermissions permissions)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.permissions = permissions;

        messageBroker.Subscribe<MercenaryServiceAccepted>(HandleMercenaryServiceAccepted);
        messageBroker.Subscribe<RequestMercenaryService>(HandleRequestMercenaryService);
        messageBroker.Subscribe<MercenaryServiceDismissalAccepted>(HandleMercenaryServiceDismissalAccepted);
        messageBroker.Subscribe<RequestMercenaryDismissalService>(HandleRequestMercenaryDismissalService);
        messageBroker.Subscribe<PlayerRelationChange>(HandlePlayerRelationChange);
        messageBroker.Subscribe<NetworkPlayerRelationChange>(HandleNetworkPlayerRelationChange);
        messageBroker.Subscribe<GiveGold>(HandleGiveGold);
        messageBroker.Subscribe<NetworkGiveGold>(HandleNetworkGiveGold);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MercenaryServiceAccepted>(HandleMercenaryServiceAccepted);
        messageBroker.Unsubscribe<RequestMercenaryService>(HandleRequestMercenaryService);
        messageBroker.Unsubscribe<MercenaryServiceDismissalAccepted>(HandleMercenaryServiceDismissalAccepted);
        messageBroker.Unsubscribe<RequestMercenaryDismissalService>(HandleRequestMercenaryDismissalService);
        messageBroker.Unsubscribe<PlayerRelationChange>(HandlePlayerRelationChange);
        messageBroker.Unsubscribe<NetworkPlayerRelationChange>(HandleNetworkPlayerRelationChange);
        messageBroker.Unsubscribe<GiveGold>(HandleGiveGold);
        messageBroker.Unsubscribe<NetworkGiveGold>(HandleNetworkGiveGold);
    }

    private void HandleMercenaryServiceAccepted(MessagePayload<MercenaryServiceAccepted> payload)
    {
        // Conversation consequences publish synchronously on the game thread.
        if (!objectManager.TryGetIdWithLogging(payload.What.Kingdom, out var kingdomId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.Clan, out var clanId)) return;

        network.SendAll(new RequestMercenaryService(kingdomId, payload.What.AwardMultiplier, clanId));
    }

    private void HandleRequestMercenaryService(MessagePayload<RequestMercenaryService> payload)
    {
        if (ModInformation.IsClient) return;

        // Peer associations use a ConcurrentDictionary and are safe to resolve on the poll thread.
        if (!(payload.Who is NetPeer peer) || !playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Error("Received {Message} without a registered player peer", nameof(RequestMercenaryService));
            return;
        }

        GameThread.RunSafe(
            () => ApplyMercenaryService(payload.What.ClanId, player.HeroId, payload.What.KingdomId, payload.What.AwardMultiplier),
            context: nameof(MercenaryServiceHandler));
    }

    private void ApplyMercenaryService(string clanId, string heroId, string kingdomId, int awardMultiplier)
    {
        // Only called from the GameThread.RunSafe action above.
        if (!objectManager.TryGetObjectWithLogging<Clan>(clanId, out var clan)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(heroId, out var hero)) return;
        if (!objectManager.TryGetObjectWithLogging<Kingdom>(kingdomId, out var kingdom)) return;
        // A ruler hires a clan into its own kingdom from the hire conversation, so it needn't lead that clan.
        if (kingdom.Leader != hero && !IsLeaderOf(hero, clan)) return;

        if (clan.Kingdom != null || clan.IsUnderMercenaryService)
        {
            Logger.Warning("Rejected mercenary service request because clan {ClanId} already belongs to a kingdom", clanId);
            return;
        }

        ChangeKingdomAction.ApplyByJoinFactionAsMercenary(clan, kingdom, default, awardMultiplier);
        if (clan == hero.Clan)
        {
            GainKingdomInfluenceAction.ApplyForJoiningFaction(hero, 5f);
        }
    }

    private void HandleMercenaryServiceDismissalAccepted(MessagePayload<MercenaryServiceDismissalAccepted> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.Kingdom, out var kingdomId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.Clan, out var clanId)) return;

        network.SendAll(new RequestMercenaryDismissalService(kingdomId, clanId));
    }

    private void HandleRequestMercenaryDismissalService(MessagePayload<RequestMercenaryDismissalService> payload)
    {
        if (ModInformation.IsClient) return;

        // Peer associations use a ConcurrentDictionary and are safe to resolve on the poll thread.
        if (!(payload.Who is NetPeer peer) || !playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Error("Received {Message} without a registered player peer", nameof(RequestMercenaryDismissalService));
            return;
        }

        GameThread.RunSafe(
            () => ApplyMercenaryDismissalService(payload.What.ClanId, player.HeroId),
            context: nameof(MercenaryServiceHandler));
    }

    private void ApplyMercenaryDismissalService(string clanId, string heroId)
    {
        if (!objectManager.TryGetObjectWithLogging<Clan>(clanId, out var clan)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(heroId, out var hero)) return;
        // A ruler ends a mercenary clan's service from the conversation, to fire one serving its own kingdom or to hire
        // one away from another kingdom, so it needn't lead that clan.
        bool rulerEndsService = clan.IsUnderMercenaryService && hero.Clan?.Kingdom != null && hero.Clan.Kingdom.Leader == hero;
        if (!rulerEndsService && !IsLeaderOf(hero, clan)) return;

        if (clan.Kingdom == null || !clan.IsUnderMercenaryService)
        {
            Logger.Warning("Rejected mercenary service removal request because clan {ClanId} does not belong to a kingdom", clanId);
            return;
        }

        // Leaving the kingdom clears the clan's own influence, as vanilla's Clan.LeaveKingdomInternal does, so a
        // fired mercenary's unpaid debt goes with it and the requester's influence is never read here.
        ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(clan, true);
    }

    // Mercenary service binds the whole clan, so apart from a ruler's hire or firing only the clan leader's player
    // may take or end it.
    private bool IsLeaderOf(Hero hero, Clan clan)
    {
        if (permissions.CanManageClan(hero, clan)) return true;

        Logger.Warning("Ignoring a mercenary service change for clan {ClanId} from a player who does not lead it", clan.StringId);
        return false;
    }
    private void HandlePlayerRelationChange(MessagePayload<PlayerRelationChange> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.Hero, out var heroId)) return;

        network.SendAll(new NetworkPlayerRelationChange(heroId, payload.What.Relation));
    }
    private void HandleNetworkPlayerRelationChange(MessagePayload<NetworkPlayerRelationChange> payload)
    {
        if (!(payload.Who is NetPeer peer) || !playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Error("Received {Message} without a registered player peer", nameof(RequestMercenaryDismissalService));
            return;
        }
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(payload.What.HeroId, out var hero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var clientHero)) return;

            ResolvedMainHeroContext.ResolvedMainHero = clientHero;
            try
            {
                ChangeRelationAction.ApplyPlayerRelation(hero, payload.What.Relation, true, true);
            }
            finally
            {
                ResolvedMainHeroContext.ResolvedMainHero = null;
            }
        }); 
    }
    private void HandleGiveGold(MessagePayload<GiveGold> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.Hero, out var heroId)) return;

        network.SendAll(new NetworkGiveGold(payload.What.Gold, heroId));
    }
    private void HandleNetworkGiveGold(MessagePayload<NetworkGiveGold> payload)
    {
        if (!(payload.Who is NetPeer peer) || !playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Error("Received {Message} without a registered player peer", nameof(NetworkGiveGold));
            return;
        }
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(payload.What.HeroId, out var hero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var clientHero)) return;

            GiveGoldAction.ApplyBetweenCharacters(clientHero, hero, payload.What.Gold, false);
        });
    }
}
