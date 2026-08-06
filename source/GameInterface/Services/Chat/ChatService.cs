using Common.Network;
using GameInterface.Services.Chat.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.Chat;

public interface IChatService : IGameAbstraction
{
    void Initialize();
    void Receive(NetworkChatMessage message);
}

/// <summary>Owns the client chat view model and overlay for one co-op session.</summary>
public sealed class ChatService : IChatService, IDisposable
{
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly ChatVM viewModel;
    private readonly ChatOverlay overlay;

    public ChatService(
        INetwork network,
        IPlayerManager playerManager,
        IObjectManager objectManager,
        IControllerIdProvider controllerIdProvider)
    {
        this.network = network;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.controllerIdProvider = controllerIdProvider;

        viewModel = new ChatVM(message => network.SendAll(message), () => controllerIdProvider.ControllerId);
        overlay = new ChatOverlay(viewModel, RefreshParticipants);
    }

    public void Initialize()
    {
        overlay.Initialize();
    }

    public void Receive(NetworkChatMessage message)
    {
        viewModel.Receive(message);
    }

    public void Dispose()
    {
        overlay.Dispose();
    }

    private void RefreshParticipants()
    {
        var participants = new List<(string ControllerId, string Name)>();
        foreach (var player in playerManager.Players)
        {
            if (string.Equals(player.ControllerId, controllerIdProvider.ControllerId, StringComparison.Ordinal))
                continue;

            participants.Add((player.ControllerId, ChatPlayerName.Resolve(objectManager, player)));
        }

        foreach (var participant in participants.OrderBy(
                     participant => participant.Name,
                     StringComparer.OrdinalIgnoreCase))
        {
            viewModel.AddParticipant(participant.ControllerId, participant.Name);
        }
    }

}
