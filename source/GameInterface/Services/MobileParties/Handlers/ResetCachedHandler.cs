using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

internal class ResetCachedHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ResetCachedHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ResetCachedHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<ResetMobilePartyCached>(Handle_ResetMobilePartyCached);
        messageBroker.Subscribe<NetworkResetMobilePartyCached>(Handle_NetworkResetMobilePartyCached);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ResetMobilePartyCached>(Handle_ResetMobilePartyCached);
        messageBroker.Unsubscribe<NetworkResetMobilePartyCached>(Handle_NetworkResetMobilePartyCached);
    }

    private void Handle_ResetMobilePartyCached(MessagePayload<ResetMobilePartyCached> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

            var message = new NetworkResetMobilePartyCached(mobilePartyId);
            network.SendAll(message);
        }); 
    }

    private void Handle_NetworkResetMobilePartyCached(MessagePayload<NetworkResetMobilePartyCached> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            mobileParty.ResetCached();
        });
    }
}
