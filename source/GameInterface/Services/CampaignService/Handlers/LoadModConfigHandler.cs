using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Configuration;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Interaces;
using LiteNetLib;
using Serilog;

namespace GameInterface.Services.CampaignService.Handlers;

internal class LoadModConfigHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LoadModConfigHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IModConfig modConfig;
    private readonly ITimeControlInterface timeControlInterface;

    public LoadModConfigHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IModConfig modConfig,
        ITimeControlInterface timeControlInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.modConfig = modConfig;
        this.timeControlInterface = timeControlInterface;
        messageBroker.Subscribe<CampaignReady>(Handle_CampaignReady);
        messageBroker.Subscribe<NetworkRequestServerModConfig>(Handle_NetworkRequestServerModConfig);
        messageBroker.Subscribe<NetworkLoadModConfig>(Handle_NetworkLoadModConfig);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignReady>(Handle_CampaignReady);
        messageBroker.Unsubscribe<NetworkRequestServerModConfig>(Handle_NetworkRequestServerModConfig);
        messageBroker.Unsubscribe<NetworkLoadModConfig>(Handle_NetworkLoadModConfig);
    }

    private void Handle_CampaignReady(MessagePayload<CampaignReady> obj)
    {
        // Use server's config
        if (ModInformation.IsClient)
        {
            network.SendAll(new NetworkRequestServerModConfig());
            return;
        }

        ModConfigProvider.LoadModConfig(modConfig.Data.ModOptions);
        ApplyConfigs();

        network.SendAll(new NetworkLoadModConfig(ModConfigProvider.ModOptions));
    }

    private void Handle_NetworkRequestServerModConfig(MessagePayload<NetworkRequestServerModConfig> obj)
    {
        GameThread.RunSafe(() =>
        {
            network.Send(obj.Who as NetPeer, new NetworkLoadModConfig(ModConfigProvider.ModOptions));
        });
    }

    private void Handle_NetworkLoadModConfig(MessagePayload<NetworkLoadModConfig> obj)
    {
        GameThread.RunSafe(() =>
        {
            ModConfigProvider.ModOptions = obj.What.ModOptions;
        });
    }

    private void ApplyConfigs()
    {
        if (!ModConfigProvider.ModOptions.FastForwardEnabled)
        {
            timeControlInterface.AddFastForwardPolicy(() => false);
        }
    }
}
