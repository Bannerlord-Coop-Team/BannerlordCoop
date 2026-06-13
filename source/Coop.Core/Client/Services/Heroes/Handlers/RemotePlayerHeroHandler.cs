using Common.Logging;
using Common.Messaging;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Heroes.Data;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Coop.Core.Client.Services.Heroes.Handlers;

/// <summary>
/// Persistent client handler for remote-player hero creation.
/// </summary>
/// <remarks>
/// Subscribes to <see cref="NetworkNewPlayerHeroCreated"/> for the whole client lifetime so the message is never
/// dropped in the gap between join states (ReceivingSavedData → Loading → Campaign) — previously a player that
/// joined while this client was loading could be lost. While the campaign is still loading, heroes are deferred;
/// once the client has entered the campaign (<see cref="ClientCampaignEntered"/>) the backlog is drained and any
/// further heroes are instantiated immediately. Leaving to the main menu resets readiness so a reconnect defers
/// again instead of instantiating against an unloaded campaign.
/// </remarks>
internal class RemotePlayerHeroHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<RemotePlayerHeroHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IHeroInterface heroInterface;
    private readonly IPlayerManager playerRegistry;
    private readonly IDeferredHeroRepository deferredHeroRepo;
    private readonly List<Player> pendingExistingPlayers = new List<Player>();
    private bool campaignReady;

    public RemotePlayerHeroHandler(
        IMessageBroker messageBroker,
        IHeroInterface heroInterface,
        IPlayerManager playerRegistry,
        IDeferredHeroRepository deferredHeroRepo)
    {
        this.messageBroker = messageBroker;
        this.heroInterface = heroInterface;
        this.playerRegistry = playerRegistry;
        this.deferredHeroRepo = deferredHeroRepo;

        messageBroker.Subscribe<NetworkNewPlayerHeroCreated>(Handle_NetworkNewPlayerHeroCreated);
        messageBroker.Subscribe<NetworkExistingPlayers>(Handle_NetworkExistingPlayers);
        messageBroker.Subscribe<ClientCampaignEntered>(Handle_ClientCampaignEntered);
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkNewPlayerHeroCreated>(Handle_NetworkNewPlayerHeroCreated);
        messageBroker.Unsubscribe<NetworkExistingPlayers>(Handle_NetworkExistingPlayers);
        messageBroker.Unsubscribe<ClientCampaignEntered>(Handle_ClientCampaignEntered);
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }

    private void Handle_NetworkNewPlayerHeroCreated(MessagePayload<NetworkNewPlayerHeroCreated> payload)
    {
        if (campaignReady)
        {
            CreatePlayerHero(payload.What);
        }
        else
        {
            // Still loading: defer until the campaign is ready so the message is never lost in a state gap.
            deferredHeroRepo.AddDeferredHero(payload.What);
        }
    }

    private void Handle_NetworkExistingPlayers(MessagePayload<NetworkExistingPlayers> payload)
    {
        // Protobuf collapses an empty array to null (fresh server, no other players yet).
        var players = payload.What.Players ?? System.Array.Empty<Player>();

        if (campaignReady)
        {
            RegisterExistingPlayers(players);
        }
        else
        {
            pendingExistingPlayers.AddRange(players);
        }
    }

    private void Handle_ClientCampaignEntered(MessagePayload<ClientCampaignEntered> payload)
    {
        campaignReady = true;

        // Players whose heroes came inside the transfer save register first; their objects
        // exist as soon as the campaign does.
        RegisterExistingPlayers(pendingExistingPlayers);
        pendingExistingPlayers.Clear();

        foreach (var message in deferredHeroRepo.GetAllDeferredHeroes())
        {
            CreatePlayerHero(message);
        }

        deferredHeroRepo.Clear();
    }

    private void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> payload)
    {
        // Left the campaign (e.g. disconnect): drop any backlog and require a fresh campaign before
        // instantiating again, so a reconnect doesn't create heroes against an unloaded campaign.
        campaignReady = false;
        deferredHeroRepo.Clear();
        pendingExistingPlayers.Clear();
    }

    private void RegisterExistingPlayers(IEnumerable<Player> players)
    {
        foreach (var player in players)
        {
            // Unlike a newly created hero, the player's objects are already inside the
            // transfer save — only the registry record is missing. Re-adding a known player
            // (this client's own, or a duplicate delivery) is expected and harmless.
            playerRegistry.AddPlayer(player);
        }
    }

    private void CreatePlayerHero(NetworkNewPlayerHeroCreated message)
    {
        var player = message.Player;

        if (!playerRegistry.AddPlayer(message.Player))
        {
            Logger.Error("Player {HeroId} has already been added.", message.Player.HeroId);
            return;
        }
            
        // Unpack + set up in one main-thread pass, registering the host's ids carried by the Player.
        heroInterface.ClientUnpackHero(message.HeroData, player);
    }
}
