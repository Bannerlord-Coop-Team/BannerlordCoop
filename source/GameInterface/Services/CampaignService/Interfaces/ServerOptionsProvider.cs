using GameInterface.Services.AdvancedStart.Interfaces;
using GameInterface.Services.CampaignService.Data;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.CampaignService.Interfaces;

public interface IServerOptionsProvider : IGameAbstraction
{
    ServerOptions GetServerOptions();
}

public class ServerOptionsProvider : IServerOptionsProvider
{
    private readonly IAdvancedStartInterface advancedStartInterface;

    public ServerOptionsProvider(IAdvancedStartInterface advancedStartInterface)
    {
        this.advancedStartInterface = advancedStartInterface;
    }

    public ServerOptions GetServerOptions()
    {
        // Expand with other options as needed
        var serverOptions = new ServerOptions(
            BannerlordConfig.PlayerReceivedDamageDifficulty,
            worldStartsOffered: advancedStartInterface.OffersWorldStarts());

        return serverOptions;
    }
}
