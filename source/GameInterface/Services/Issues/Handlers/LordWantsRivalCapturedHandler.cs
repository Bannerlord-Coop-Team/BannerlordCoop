using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using LiteNetLib;

namespace GameInterface.Services.Issues.Handlers;

internal sealed class LordWantsRivalCapturedHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly ILordWantsRivalCapturedQuestService quests;

    public LordWantsRivalCapturedHandler(IMessageBroker messageBroker, IPlayerManager playerManager,
        ILordWantsRivalCapturedQuestService quests)
    {
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.quests = quests;
        messageBroker.Subscribe<RivalCapturedIssueCreated>(HandleCreated);
        messageBroker.Subscribe<NetworkRivalCapturedIssueCreated>(HandleNetworkCreated);
        messageBroker.Subscribe<RequestRivalCapturedChoice>(HandleChoice);
        messageBroker.Subscribe<NetworkRivalCapturedProgress>(HandleProgress);
        messageBroker.Subscribe<NetworkRivalCapturedTraitProgress>(HandleTraitProgress);
        messageBroker.Subscribe<PlayerHeroChanged>(HandlePlayerHeroChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RivalCapturedIssueCreated>(HandleCreated);
        messageBroker.Unsubscribe<NetworkRivalCapturedIssueCreated>(HandleNetworkCreated);
        messageBroker.Unsubscribe<RequestRivalCapturedChoice>(HandleChoice);
        messageBroker.Unsubscribe<NetworkRivalCapturedProgress>(HandleProgress);
        messageBroker.Unsubscribe<NetworkRivalCapturedTraitProgress>(HandleTraitProgress);
        messageBroker.Unsubscribe<PlayerHeroChanged>(HandlePlayerHeroChanged);
    }

    private void HandleCreated(MessagePayload<RivalCapturedIssueCreated> payload)
    {
        if (ModInformation.IsServer) quests.BroadcastCreation(payload.What.Issue);
    }

    private void HandleNetworkCreated(MessagePayload<NetworkRivalCapturedIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() => quests.MirrorCreation(payload.What));
    }

    private void HandleChoice(MessagePayload<RequestRivalCapturedChoice> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            if (payload.Who is NetPeer peer && playerManager.TryGetPlayer(peer, out var player))
                quests.ApplyChoice(player, payload.What);
        });
    }

    private void HandleProgress(MessagePayload<NetworkRivalCapturedProgress> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() => quests.MirrorProgress(payload.What));
    }

    private void HandleTraitProgress(MessagePayload<NetworkRivalCapturedTraitProgress> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() => quests.MirrorTraitProgress(payload.What));
    }

    private void HandlePlayerHeroChanged(MessagePayload<PlayerHeroChanged> payload)
    {
        if (ModInformation.IsClient) quests.RestoreLocalTraitProgress(payload.What.NewHero);
    }
}
