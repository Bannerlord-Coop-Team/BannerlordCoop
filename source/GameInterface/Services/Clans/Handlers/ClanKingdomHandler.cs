using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Handlers;

internal class ClanKingdomHandler : IHandler
{
    private readonly ILogger Logger = LogManager.GetLogger<ClanKingdomHandler>();

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

        messageBroker.Subscribe<ClanEntersKingdom>(Handle_ClanEntersKingdom);
        messageBroker.Subscribe<NetworkClanEntersKingdom>(Handle_NetworkClanEntersKingdom);

        messageBroker.Subscribe<ClanLeavesKingdom>(Handle_ClanLeavesKingdom);
        messageBroker.Subscribe<NetworkClanLeavesKingdom>(Handle_NetworkClanLeavesKingdom);

        messageBroker.Subscribe<UpdateBannerColorsOfClan>(Handle_UpdateBannerColorsOfClan);
        messageBroker.Subscribe<NetworkUpdateBannerColorsOfClan>(Handle_NetworkUpdateBannerColorsOfClan);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClanEntersKingdom>(Handle_ClanEntersKingdom);
        messageBroker.Unsubscribe<NetworkClanEntersKingdom>(Handle_NetworkClanEntersKingdom);

        messageBroker.Unsubscribe<ClanLeavesKingdom>(Handle_ClanLeavesKingdom);
        messageBroker.Unsubscribe<NetworkClanLeavesKingdom>(Handle_NetworkClanLeavesKingdom);

        messageBroker.Unsubscribe<UpdateBannerColorsOfClan>(Handle_UpdateBannerColorsOfClan);
        messageBroker.Unsubscribe<NetworkUpdateBannerColorsOfClan>(Handle_NetworkUpdateBannerColorsOfClan);
    }

    private void Handle_ClanEntersKingdom(MessagePayload<ClanEntersKingdom> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.Clan, out var clanId)) return;

        network.SendAll(new NetworkClanEntersKingdom(clanId));
    }

    private void Handle_NetworkClanEntersKingdom(MessagePayload<NetworkClanEntersKingdom> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.ClanId, out var clan)) return;

            using (new AllowedThread())
            {
                clan._kingdom.AddClanInternal(clan);
            }
        });
    }

    private void Handle_ClanLeavesKingdom(MessagePayload<ClanLeavesKingdom> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.Clan, out var clanId)) return;

        network.SendAll(new NetworkClanEntersKingdom(clanId));
    }

    private void Handle_NetworkClanLeavesKingdom(MessagePayload<NetworkClanLeavesKingdom> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.ClanId, out var clan)) return;

            using (new AllowedThread())
            {
                clan._kingdom.RemoveClanInternal(clan);
            }
        });
    }

    private void Handle_UpdateBannerColorsOfClan(MessagePayload<UpdateBannerColorsOfClan> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.Clan, out var clanId)) return;

        network.SendAll(new NetworkUpdateBannerColorsOfClan(clanId));
    }

    private void Handle_NetworkUpdateBannerColorsOfClan(MessagePayload<NetworkUpdateBannerColorsOfClan> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.ClanId, out var clan)) return;

            using (new AllowedThread())
            {
                clan.UpdateBannerColorsAccordingToKingdom();
            }
        });
    }
}
