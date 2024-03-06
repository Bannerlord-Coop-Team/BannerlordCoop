using Common.Logging;
using Common.Messaging;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.CampaignService.Patches;
using GameInterface.Services.Clans.Handlers;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.CampaignService.Handlers;
public class CampaignHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ILogger Logger = LogManager.GetLogger<CampaignHandler>();

    public CampaignHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<ChangeCampaignTime>(HandleHourlyTick);
        // messageBroker.Subscribe<CampaignTimeChanged>(HandleTimeChange);
    }

    private void HandleHourlyTick(MessagePayload<ChangeCampaignTime> payload)
    {
        var obj = payload.What;

        CampaignTimePatch.RunTimeChange(obj.NumTicks, obj.DeltaTime);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeCampaignTime>(HandleHourlyTick);


    }
}
