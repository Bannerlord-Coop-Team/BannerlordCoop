using GameInterface.Services.CampaignService.Data;
using GameInterface.Services.MapEvents.BattleSize;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.CampaignService.Interfaces;

public interface IServerOptionsProvider : IGameAbstraction
{
    ServerOptions GetServerOptions();

    void ApplyServerOptions(ServerOptions serverOptions);
}

public class ServerOptionsProvider : IServerOptionsProvider
{
    private readonly IServerBattleSizeProvider battleSizeProvider;

    public ServerOptionsProvider(IServerBattleSizeProvider battleSizeProvider)
    {
        this.battleSizeProvider = battleSizeProvider;
    }

    public ServerOptions GetServerOptions()
    {
        var serverOptions = new ServerOptions(
            BannerlordConfig.PlayerReceivedDamageDifficulty,
            battleSizeProvider.BattleSize);

        return serverOptions;
    }

    public void ApplyServerOptions(ServerOptions serverOptions)
    {
        BannerlordConfig.PlayerReceivedDamageDifficulty = serverOptions.PlayerReceivedDamage;
        battleSizeProvider.SetBattleSize(serverOptions.BattleSize);
    }
}
