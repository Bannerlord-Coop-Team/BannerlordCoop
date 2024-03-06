using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.CampaignServices.Handler;
using Coop.Core.Server.Services.CampaignServices.Messages;
using GameInterface.Services.CampaignService.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.CampaignService.Handler;
internal class ClientCampaignHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ILogger Logger = LogManager.GetLogger<ClientCampaignHandler>();

    public ClientCampaignHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        messageBroker.Subscribe<NetworkCampaignTimeChanged>(HandleHourlyTick);
    }

    private void HandleHourlyTick(MessagePayload<NetworkCampaignTimeChanged> payload)
    {
        var obj = payload.What;

        messageBroker.Publish(this, new ChangeCampaignTime(obj.NumTicks, obj.DeltaTime));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkCampaignTimeChanged>(HandleHourlyTick);

    }
}
