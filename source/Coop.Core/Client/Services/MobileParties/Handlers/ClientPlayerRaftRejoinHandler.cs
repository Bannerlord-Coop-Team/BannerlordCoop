using Common;
using Common.Messaging;
using Coop.Core.Client.Messages;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Core.Client.Services.MobileParties.Handlers;

internal class ClientPlayerRaftRejoinHandler : IHandler
{
    private readonly IMessageBroker messageBroker;

    public ClientPlayerRaftRejoinHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<ClientCampaignReady>(Handle_ClientCampaignReady);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClientCampaignReady>(Handle_ClientCampaignReady);
    }

    private void Handle_ClientCampaignReady(MessagePayload<ClientCampaignReady> payload)
    {
        if (!ModInformation.IsNavalEnabled) return;

        GameThread.RunSafe(() =>
        {
            if (!MobileParty.MainParty.IsInRaftState) return;

            GameMenu.ActivateGameMenu("player_raft_state");
        });
    }
}
