using Common.Logging;
using Common.Messaging;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Heroes.Data;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Serilog;
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
    private readonly IObjectManager objectManager;
    private readonly IHeroInterface heroInterface;
    private readonly IPlayerRegistry playerRegistry;
    private readonly IDeferredHeroRepository deferredHeroRepo;
    private bool campaignReady;

    public RemotePlayerHeroHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IHeroInterface heroInterface,
        IPlayerRegistry playerRegistry,
        IDeferredHeroRepository deferredHeroRepo)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.heroInterface = heroInterface;
        this.playerRegistry = playerRegistry;
        this.deferredHeroRepo = deferredHeroRepo;

        messageBroker.Subscribe<NetworkNewPlayerHeroCreated>(Handle_NetworkNewPlayerHeroCreated);
        messageBroker.Subscribe<ClientCampaignEntered>(Handle_ClientCampaignEntered);
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkNewPlayerHeroCreated>(Handle_NetworkNewPlayerHeroCreated);
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

    private void Handle_ClientCampaignEntered(MessagePayload<ClientCampaignEntered> payload)
    {
        campaignReady = true;

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
    }

    private void CreatePlayerHero(NetworkNewPlayerHeroCreated message)
    {
        var player = message.Player;

        var hero = heroInterface.UnpackHero(message.HeroData);

        objectManager.AddExisting(player.HeroId, hero);
        objectManager.AddExisting(player.MobilePartyId, hero.PartyBelongedTo);
        objectManager.AddExisting(player.ClanId, hero.Clan);
        objectManager.AddExisting(player.CharacterObjectId, hero.CharacterObject);

        heroInterface.SetupNewHero(hero);

        if (!playerRegistry.AddPlayer(message.Player))
        {
            Logger.Error("Player {HeroId} has already been added.", message.Player.HeroId);
            return;
        }
    }
}
