using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.CampaignServices.Messages;
using GameInterface.Services.CampaignService.Messages;
using Serilog;
using System;

namespace Coop.Core.Server.Services.CampaignServices.Handler;
public class ServerCampaignHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ILogger Logger = LogManager.GetLogger<ServerCampaignHandler>();

    public ServerCampaignHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        messageBroker.Subscribe<CampaignTimeChanged>(HandleTimeChange);
    }

    private void HandleTimeChange(MessagePayload<CampaignTimeChanged> payload)
    {
        var obj = payload.What;

        network.SendAll(new NetworkCampaignTimeChanged(obj.NumTicks, obj.DeltaTime));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignTimeChanged>(HandleTimeChange);

    }
}
