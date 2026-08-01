using Common.Logging;
using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.GameState.Messages;
using Serilog;

namespace GameInterface.Services.CampaignService.Handlers;

internal class LoadModConfigHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LoadModConfigHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IModConfig modConfig;

    public LoadModConfigHandler(
        IMessageBroker messageBroker,
        IModConfig modConfig)
    {
        this.messageBroker = messageBroker;
        this.modConfig = modConfig;

        messageBroker.Subscribe<CampaignReady>(Handle_CampaignReady);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignReady>(Handle_CampaignReady);
    }

    private void Handle_CampaignReady(MessagePayload<CampaignReady> obj)
    {
        ModConfigProvider.LoadModConfig(modConfig.Data);
    }
}
