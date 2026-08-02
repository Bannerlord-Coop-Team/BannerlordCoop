using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Handlers;

internal class ClanKingdomHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(KingdomInterface));
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ClanKingdomHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    { 
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<SetClanKingdom>(HandleSetClanKingdom);
        messageBroker.Subscribe<NetworkSetClanKingdom>(HandleNetworkSetClanKingdom);
        messageBroker.Subscribe<OnClanChangedKingdom>(HandleOnClanChangedKingdom);
        messageBroker.Subscribe<NetworkOnClanChangedKingdom>(HandleNetworkOnClanChangedKingdom);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SetClanKingdom>(HandleSetClanKingdom);
        messageBroker.Unsubscribe<NetworkSetClanKingdom>(HandleNetworkSetClanKingdom);
        messageBroker.Unsubscribe<OnClanChangedKingdom>(HandleOnClanChangedKingdom);
        messageBroker.Unsubscribe<NetworkOnClanChangedKingdom>(HandleNetworkOnClanChangedKingdom);
    }

    private void HandleSetClanKingdom(MessagePayload<SetClanKingdom> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.Clan, out var clanId)) return;
        string kingdomId = null;
        if (payload.What.Kingdom != null)
        {
            if (!objectManager.TryGetIdWithLogging(payload.What.Kingdom, out kingdomId)) return;
        }

        network.SendAll(new NetworkSetClanKingdom(clanId, kingdomId));
    }

    private void HandleNetworkSetClanKingdom(MessagePayload<NetworkSetClanKingdom> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(payload.What.ClanId, out var clan)) return;
            Kingdom kingdom = null;
            if (payload.What.KingdomId != null)
            {
                if (!objectManager.TryGetObjectWithLogging<Kingdom>(payload.What.KingdomId, out kingdom)) return;
            }

            using (new AllowedThread())
            {
                clan.SetKingdomInternal(kingdom);
            }
        });
    }

    private void HandleOnClanChangedKingdom(MessagePayload<OnClanChangedKingdom> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.Clan, out var clanId)) return;
        string oldKingdomId = null;
        if (payload.What.OldKingdom != null)
        {
            if (!objectManager.TryGetIdWithLogging(payload.What.OldKingdom, out oldKingdomId)) return;
        }
        string newKingdomId = null;
        if (payload.What.NewKingdom != null)
        {
            if (!objectManager.TryGetIdWithLogging(payload.What.NewKingdom, out newKingdomId)) return;
        }

        network.SendAll(new NetworkOnClanChangedKingdom(clanId, oldKingdomId, newKingdomId, payload.What.Detail));
    }

    private void HandleNetworkOnClanChangedKingdom(MessagePayload<NetworkOnClanChangedKingdom> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(payload.What.ClanId, out var clan)) return;
            Kingdom oldKingdom = null;
            if (payload.What.OldKingdomId != null)
            {
                if (!objectManager.TryGetObjectWithLogging<Kingdom>(payload.What.OldKingdomId, out oldKingdom)) return;
            }
            Kingdom newKingdom = null;
            if (payload.What.NewKingdomId != null)
            {
                if (!objectManager.TryGetObjectWithLogging<Kingdom>(payload.What.NewKingdomId, out newKingdom)) return;
            }

            CampaignEventDispatcher.Instance.OnClanChangedKingdom(clan, oldKingdom, newKingdom, payload.What.Detail, true);
        });
    }
}
