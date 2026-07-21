using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.ServerOptions;
using GameInterface.Services.UI.Messages;

namespace GameInterface.Services.MapEvents.BattleSize;

/// <summary>Loads, applies, and distributes the server-authoritative battle size.</summary>
public class ServerBattleSizeSyncHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IServerBattleSizeProvider battleSizeProvider;

    public ServerBattleSizeSyncHandler(
        IMessageBroker messageBroker,
        INetwork network,
        ICoopOptionsStore optionsStore,
        IServerBattleSizeProvider battleSizeProvider)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.battleSizeProvider = battleSizeProvider;

        messageBroker.Subscribe<ServerBattleSizeSelected>(Handle_ServerBattleSizeSelected);
        messageBroker.Subscribe<NetworkBattleSizeChanged>(Handle_NetworkBattleSizeChanged);

        if (ModInformation.IsServer)
        {
            battleSizeProvider.SetBattleSize(
                ServerOptionsTabProvider.GetBattleSizeOrDefault(optionsStore.LoadOrDefault()));
        }
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ServerBattleSizeSelected>(Handle_ServerBattleSizeSelected);
        messageBroker.Unsubscribe<NetworkBattleSizeChanged>(Handle_NetworkBattleSizeChanged);
    }

    private void Handle_ServerBattleSizeSelected(MessagePayload<ServerBattleSizeSelected> payload)
    {
        if (ModInformation.IsClient) return;

        battleSizeProvider.SetBattleSize(payload.What.BattleSize);
        network.SendAll(new NetworkBattleSizeChanged(battleSizeProvider.BattleSize));
    }

    private void Handle_NetworkBattleSizeChanged(MessagePayload<NetworkBattleSizeChanged> payload)
    {
        if (ModInformation.IsServer) return;

        battleSizeProvider.SetBattleSize(payload.What.BattleSize);
    }
}
