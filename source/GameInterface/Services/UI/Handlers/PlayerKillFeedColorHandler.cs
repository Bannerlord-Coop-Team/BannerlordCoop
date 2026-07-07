using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab;
using GameInterface.Services.UI.Messages;
using LiteNetLib;
using Serilog;
using System.Linq;

namespace GameInterface.Services.UI.Handlers;

public class PlayerKillFeedColorHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerKillFeedColorHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IPlayerKillFeedColorService colorService;
    private readonly ICoopOptionsStore optionsStore;
    private readonly IControllerIdProvider controllerIdProvider;

    public PlayerKillFeedColorHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IPlayerManager playerManager,
        IPlayerKillFeedColorService colorService,
        ICoopOptionsStore optionsStore,
        IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.playerManager = playerManager;
        this.colorService = colorService;
        this.optionsStore = optionsStore;
        this.controllerIdProvider = controllerIdProvider;

        messageBroker.Subscribe<PlayerKillFeedColorSelected>(Handle_PlayerKillFeedColorSelected);
        messageBroker.Subscribe<PlayerKillFeedColorResendRequested>(Handle_PlayerKillFeedColorResendRequested);
        messageBroker.Subscribe<NetworkRequestKillFeedColor>(Handle_NetworkRequestKillFeedColor);
        messageBroker.Subscribe<NetworkUpdateKillFeedColor>(Handle_NetworkUpdateKillFeedColor);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerKillFeedColorSelected>(Handle_PlayerKillFeedColorSelected);
        messageBroker.Unsubscribe<PlayerKillFeedColorResendRequested>(Handle_PlayerKillFeedColorResendRequested);
        messageBroker.Unsubscribe<NetworkRequestKillFeedColor>(Handle_NetworkRequestKillFeedColor);
        messageBroker.Unsubscribe<NetworkUpdateKillFeedColor>(Handle_NetworkUpdateKillFeedColor);
    }

    private void Handle_PlayerKillFeedColorSelected(MessagePayload<PlayerKillFeedColorSelected> payload)
    {
        if (ModInformation.IsServer) return;

        var color = payload.What.Color;

        CacheLocalColor(color);
        network.SendAll(new NetworkRequestKillFeedColor(color.Red, color.Green, color.Blue));
    }

    private void Handle_PlayerKillFeedColorResendRequested(MessagePayload<PlayerKillFeedColorResendRequested> payload)
    {
        if (ModInformation.IsServer) return;
        if (!optionsStore.TryLoad(out var options)) return;
        if (!KillFeedOptionsTabProvider.TryGetKillFeedColor(options, out var color)) return;

        CacheLocalColor(color);
        network.SendAll(new NetworkRequestKillFeedColor(color.Red, color.Green, color.Blue));
    }

    private void Handle_NetworkRequestKillFeedColor(MessagePayload<NetworkRequestKillFeedColor> payload)
    {
        if (ModInformation.IsClient) return;

        var request = payload.What;
        if (!PlayerKillFeedColor.TryCreate(request.Red, request.Green, request.Blue, out var color))
        {
            Logger.Warning("Ignoring invalid kill-feed color request: {Red}, {Green}, {Blue}",
                request.Red, request.Green, request.Blue);
            return;
        }

        if (payload.Who is not NetPeer peer)
        {
            Logger.Warning("Ignoring kill-feed color request without a network peer");
            return;
        }

        if (!playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Warning("Ignoring kill-feed color request from an unregistered peer");
            return;
        }

        foreach (var knownColor in colorService.GetColors().Where(kvp => kvp.Key != player.ControllerId))
        {
            network.Send(peer, new NetworkUpdateKillFeedColor(
                knownColor.Key,
                knownColor.Value.Red,
                knownColor.Value.Green,
                knownColor.Value.Blue));
        }

        colorService.SetColor(player.ControllerId, color);
        network.SendAll(new NetworkUpdateKillFeedColor(player.ControllerId, color.Red, color.Green, color.Blue));
    }

    private void Handle_NetworkUpdateKillFeedColor(MessagePayload<NetworkUpdateKillFeedColor> payload)
    {
        if (ModInformation.IsServer) return;

        var update = payload.What;
        if (string.IsNullOrEmpty(update.ControllerId)) return;
        if (!PlayerKillFeedColor.TryCreate(update.Red, update.Green, update.Blue, out var color)) return;

        colorService.SetColor(update.ControllerId, color);
    }

    private void CacheLocalColor(PlayerKillFeedColor color)
    {
        if (string.IsNullOrEmpty(controllerIdProvider.ControllerId)) return;

        colorService.SetColor(controllerIdProvider.ControllerId, color);
    }
}
