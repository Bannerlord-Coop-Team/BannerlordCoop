using Common.Logging;
using Common.Messaging;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Players;
using Serilog;

namespace Coop.Core.Client.Services.Heroes.Handlers;

/// <summary>
/// Persistent client handler that instantiates a remote player's hero when the server announces one.
/// </summary>
/// <remarks>
/// The server's per-peer connection message queue withholds <see cref="NetworkNewPlayerHeroCreated"/>
/// (and every other world broadcast) from a client until that client reports it has entered the
/// campaign. By the time this handler sees the message the campaign save is loaded and the hero, party
/// and clan ids the message references are already registered, so the hero can be instantiated
/// immediately — there is no client-side deferral here. Reconnect correctness relies on the coop
/// container being finalized on disconnect, which yields a fresh handler per session; this handler no
/// longer resets itself when leaving to the main menu.
/// </remarks>
internal class RemotePlayerHeroHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<RemotePlayerHeroHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IHeroInterface heroInterface;
    private readonly IPlayerManager playerRegistry;

    public RemotePlayerHeroHandler(
        IMessageBroker messageBroker,
        IHeroInterface heroInterface,
        IPlayerManager playerRegistry)
    {
        this.messageBroker = messageBroker;
        this.heroInterface = heroInterface;
        this.playerRegistry = playerRegistry;

        messageBroker.Subscribe<NetworkNewPlayerHeroCreated>(Handle_NetworkNewPlayerHeroCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkNewPlayerHeroCreated>(Handle_NetworkNewPlayerHeroCreated);
    }

    private void Handle_NetworkNewPlayerHeroCreated(MessagePayload<NetworkNewPlayerHeroCreated> payload)
    {
        var message = payload.What;
        var player = message.Player;

        if (!playerRegistry.AddPlayer(player))
        {
            // The queue should deliver this exactly once, after the campaign is ready. A repeat here
            // signals a delivery hole (a broadcast that reached this peer outside the queue) rather
            // than normal operation, so keep it observable.
            Logger.Error("Player {HeroId} has already been added.", player.HeroId);
            return;
        }

        // Unpack + set up in one main-thread pass, registering the host's ids carried by the Player.
        heroInterface.ClientUnpackHero(message.HeroData, player);
    }
}
