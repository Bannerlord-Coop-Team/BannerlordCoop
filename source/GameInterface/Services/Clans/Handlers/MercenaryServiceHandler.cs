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

    public MercenaryServiceHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;

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

        network.SendAll(new RequestMercenaryService(kingdomId, payload.What.AwardMultiplier));
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
            () => ApplyMercenaryService(player.ClanId, player.HeroId, payload.What.KingdomId, payload.What.AwardMultiplier),
            context: nameof(MercenaryServiceHandler));
    }

    private void ApplyMercenaryService(string clanId, string heroId, string kingdomId, int awardMultiplier)
    {
        // Only called from the GameThread.RunSafe action above.
        if (!objectManager.TryGetObjectWithLogging<Clan>(clanId, out var clan)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(heroId, out var hero)) return;
        if (!objectManager.TryGetObjectWithLogging<Kingdom>(kingdomId, out var kingdom)) return;

        if (hero.Clan != clan)
        {
            Logger.Warning("Rejected mercenary service request because hero {HeroId} does not belong to clan {ClanId}", heroId, clanId);
            return;
        }

        if (clan.Kingdom != null || clan.IsUnderMercenaryService)
        {
            Logger.Warning("Rejected mercenary service request because clan {ClanId} already belongs to a kingdom", clanId);
            return;
        }

        ChangeKingdomAction.ApplyByJoinFactionAsMercenary(clan, kingdom, default, awardMultiplier);
        GainKingdomInfluenceAction.ApplyForJoiningFaction(hero, 5f);
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

        if (clan.Kingdom == null || !clan.IsUnderMercenaryService)
        {
            Logger.Warning("Rejected mercenary service removal request because clan {ClanId} does not belong to a kingdom", clanId);
            return;
        }

        ChangeClanInfluenceAction.Apply(clan, -hero.Clan.Influence);
        ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(clan, true);
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

        network.SendAll(new NetworkPlayerRelationChange(heroId, payload.What.Gold));
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
