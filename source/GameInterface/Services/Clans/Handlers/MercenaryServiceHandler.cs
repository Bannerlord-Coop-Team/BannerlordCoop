using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages;
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
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MercenaryServiceAccepted>(HandleMercenaryServiceAccepted);
        messageBroker.Unsubscribe<RequestMercenaryService>(HandleRequestMercenaryService);
    }

    private void HandleMercenaryServiceAccepted(MessagePayload<MercenaryServiceAccepted> payload)
    {
        // Conversation consequences publish synchronously on the game thread.
        if (!objectManager.TryGetIdWithLogging(payload.What.Kingdom, out var kingdomId)) return;

        network.SendAll(new RequestMercenaryService(kingdomId));
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
            () => ApplyMercenaryService(player.ClanId, player.HeroId, payload.What.KingdomId),
            context: nameof(MercenaryServiceHandler));
    }

    private void ApplyMercenaryService(string clanId, string heroId, string kingdomId)
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

        int awardMultiplier = Campaign.Current.Models.MinorFactionsModel.GetMercenaryAwardFactorToJoinKingdom(clan, kingdom);

        ChangeKingdomAction.ApplyByJoinFactionAsMercenary(clan, kingdom, default, awardMultiplier);
        GainKingdomInfluenceAction.ApplyForJoiningFaction(hero, 5f);
    }
}
