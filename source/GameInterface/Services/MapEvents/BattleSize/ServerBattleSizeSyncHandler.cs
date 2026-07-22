using Common;
using Common.Messaging;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.ServerOptions;
using GameInterface.Services.UI.Messages;

namespace GameInterface.Services.MapEvents.BattleSize;

/// <summary>Loads, applies, and distributes the server-authoritative battle size.</summary>
public class ServerBattleSizeSyncHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IServerBattleSizeProvider battleSizeProvider;

    public ServerBattleSizeSyncHandler(
        IMessageBroker messageBroker,
        ICoopOptionsStore optionsStore,
        IServerBattleSizeProvider battleSizeProvider)
    {
        this.messageBroker = messageBroker;
        this.battleSizeProvider = battleSizeProvider;

        messageBroker.Subscribe<ServerBattleSizeSelected>(Handle_ServerBattleSizeSelected);

        if (ModInformation.IsServer)
        {
            battleSizeProvider.SetBattleSize(
                ServerOptionsTabProvider.GetBattleSizeOrDefault(optionsStore.LoadOrDefault()));
        }
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ServerBattleSizeSelected>(Handle_ServerBattleSizeSelected);
    }

    private void Handle_ServerBattleSizeSelected(MessagePayload<ServerBattleSizeSelected> payload)
    {
        if (ModInformation.IsClient) return;

        battleSizeProvider.SetBattleSize(payload.What.BattleSize);
        messageBroker.Publish(this, new UpdateOtherOptions());
    }
}
