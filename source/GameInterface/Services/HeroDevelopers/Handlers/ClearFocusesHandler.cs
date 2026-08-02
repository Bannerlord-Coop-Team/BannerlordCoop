using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.HeroDevelopers.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.HeroDevelopers.Handlers;

internal class ClearFocusesHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClearFocusesHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ClearFocusesHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<ClearFocuses>(Handle_ClearFocuses);
        messageBroker.Subscribe<NetworkClearFocuses>(Handle_NetworkClearFocuses);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClearFocuses>(Handle_ClearFocuses);
        messageBroker.Unsubscribe<NetworkClearFocuses>(Handle_NetworkClearFocuses);
    }

    private void Handle_ClearFocuses(MessagePayload<ClearFocuses> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.HeroDeveloper, out var heroDeveloperId)) return;

            var message = new NetworkClearFocuses(heroDeveloperId);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkClearFocuses(MessagePayload<NetworkClearFocuses> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<HeroDeveloper>(data.HeroDeveloperId, out var heroDeveloper)) return;

            heroDeveloper.ClearFocuses();
        });
    }
}
