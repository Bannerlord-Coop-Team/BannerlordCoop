using GameInterface.Services.CampaignService.Data;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.CampaignService.Interfaces;

public interface IServerOptionsProvider : IGameAbstraction
{
    ServerOptions GetServerOptions();
}

public class ServerOptionsProvider : IServerOptionsProvider
{
    public ServerOptions GetServerOptions()
    {
        // Expand with other options as needed
        var serverOptions = new ServerOptions(
            BannerlordConfig.PlayerReceivedDamageDifficulty);

        return serverOptions;
    }
}