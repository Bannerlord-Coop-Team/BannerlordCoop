using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Handlers;

internal class ClanMercenaryServiceHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ClanMercenaryServiceHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<ClanMercenaryServiceChanged>(Handle_ClanMercenaryServiceChanged);
        messageBroker.Subscribe<NetworkClanMercenaryServiceChanged>(Handle_NetworkClanMercenaryServiceChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClanMercenaryServiceChanged>(Handle_ClanMercenaryServiceChanged);
        messageBroker.Unsubscribe<NetworkClanMercenaryServiceChanged>(Handle_NetworkClanMercenaryServiceChanged);
    }

    private void Handle_ClanMercenaryServiceChanged(MessagePayload<ClanMercenaryServiceChanged> obj)
    {
        var data = obj.What;
        
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.Clan, out var clanId)) return;

            network.SendAll(new NetworkClanMercenaryServiceChanged(clanId, data.IsUnderMercenaryService));
        });
    }

    private void Handle_NetworkClanMercenaryServiceChanged(MessagePayload<NetworkClanMercenaryServiceChanged> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.ClanId, out var clan)) return;

            using (new AllowedThread())
            {
                clan.IsUnderMercenaryService = data.IsUnderMercenaryService;
            }
        });
    }
}
