using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Players;
using Serilog;

namespace Coop.Core.Client.Services.Heroes.Handlers;

/// <summary>
/// Persistent client handler that registers a remote player when the server announces one.
/// </summary>
/// <remarks>
/// Covers both cases with one message: a player who joined after us carries its hero blob, which we unpack;
/// a player who was already in the session when we joined carries none — its hero is already in the save we
/// loaded — so we only register control.
///
/// The server's per-peer connection message queue withholds <see cref="NetworkNewPlayerHeroCreated"/> (and
/// every other world broadcast) from a client until it reports entering the campaign, so by the time this
/// handler runs the save is loaded and the referenced ids are registered. Reconnect correctness relies on
/// the coop container being finalized on disconnect, which yields a fresh handler per session.
/// </remarks>
internal class RemotePlayerHeroHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<RemotePlayerHeroHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IHeroInterface heroInterface;
    private readonly IPlayerManager playerRegistry;
    private readonly IPlayerCreationRollback playerCreationRollback;

    public RemotePlayerHeroHandler(
        IMessageBroker messageBroker,
        IHeroInterface heroInterface,
        IPlayerManager playerRegistry,
        IPlayerCreationRollback playerCreationRollback)
    {
        this.messageBroker = messageBroker;
        this.heroInterface = heroInterface;
        this.playerRegistry = playerRegistry;
        this.playerCreationRollback = playerCreationRollback;

        messageBroker.Subscribe<NetworkNewPlayerHeroCreated>(Handle_NetworkNewPlayerHeroCreated);
        messageBroker.Subscribe<NetworkPlayerCreationRolledBack>(Handle_NetworkPlayerCreationRolledBack);
        messageBroker.Subscribe<NetworkPlayerRegistrationUpdated>(Handle_NetworkPlayerRegistrationUpdated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkNewPlayerHeroCreated>(Handle_NetworkNewPlayerHeroCreated);
        messageBroker.Unsubscribe<NetworkPlayerCreationRolledBack>(Handle_NetworkPlayerCreationRolledBack);
        messageBroker.Unsubscribe<NetworkPlayerRegistrationUpdated>(Handle_NetworkPlayerRegistrationUpdated);
    }

    private void Handle_NetworkPlayerCreationRolledBack(MessagePayload<NetworkPlayerCreationRolledBack> payload)
    {
        var player = payload.What.Player;

        // Blocking preserves the reliable-stream order when the same controller retries immediately.
        GameThread.RunSafe(() =>
        {
            if (playerRegistry.TryGetPlayer(player.ControllerId, out var registeredPlayer))
                playerRegistry.RemovePlayer(registeredPlayer);

            using (new AllowedThread())
                playerCreationRollback.Rollback(player, payload.What.RegistrationIds);
        }, blocking: true, context: nameof(Handle_NetworkPlayerCreationRolledBack));
    }

    private void Handle_NetworkNewPlayerHeroCreated(MessagePayload<NetworkNewPlayerHeroCreated> payload)
    {
        var message = payload.What;
        var player = message.Player;

        // Delivered once per remote player; guard duplicates before doing any work.
        if (playerRegistry.TryGetPlayer(player.ControllerId, out _))
        {
            Logger.Error("Player {HeroId} has already been added.", player.HeroId);
            return;
        }

        // Run on the main thread: unpack (if a hero blob was sent) THEN register control. AddPlayer
        // resolves the hero/party/clan to mark them controlled, so they must already exist — both the
        // unpack and the local player's own registration also run here.
        GameThread.Run(() =>
        {
            if (message.HeroData != null && message.HeroData.Length > 0)
                heroInterface.ClientUnpackHero(message.HeroData, player);

            if (!playerRegistry.AddPlayer(player))
                Logger.Error("Player {HeroId} has already been added.", player.HeroId);
        }, blocking: true);
    }

    private void Handle_NetworkPlayerRegistrationUpdated(MessagePayload<NetworkPlayerRegistrationUpdated> payload)
    {
        var replacement = payload.What.Player;

        // Party creation and this update both defer from the ordered receive stream to the same
        // game-thread queue, so the replacement resolves after its party has been registered.
        GameThread.RunSafe(() =>
        {
            if (!playerRegistry.TryGetPlayer(replacement.ControllerId, out var registered) ||
                !playerRegistry.ReplacePlayer(registered, replacement))
            {
                Logger.Error(
                    "Could not replace player registration for controller {ControllerId}",
                    replacement.ControllerId);
            }
        }, blocking: true, context: nameof(Handle_NetworkPlayerRegistrationUpdated));
    }
}
