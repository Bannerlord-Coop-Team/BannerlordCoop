// Ignore Spelling: Finalizer

using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Common;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Registry;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.UI.Interfaces;

namespace Coop.Core.Client.States;

/// <summary>
/// State controller for the character creation client state
/// </summary>
public class CharacterCreationState : ClientStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IHeroInterface heroInterface;
    private readonly IRegistryManager registryManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly ILoadingInterface loadingInterface;
    private readonly IPlayerManager playerManager;
    private readonly IGameStateInterface gameStateInterface;
    private readonly ICoopFinalizer coopFinalizer;

    public CharacterCreationState(
        IClientLogic logic,
        IMessageBroker messageBroker,
        INetwork network,
        IHeroInterface heroInterface,
        IRegistryManager registryManager,
        IControllerIdProvider controllerIdProvider,
        ILoadingInterface loadingInterface,
        IPlayerManager playerManager,
        IGameStateInterface gameStateInterface,
        ICoopFinalizer coopFinalizer) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.heroInterface = heroInterface;
        this.registryManager = registryManager;
        this.controllerIdProvider = controllerIdProvider;
        this.loadingInterface = loadingInterface;
        this.playerManager = playerManager;
        this.gameStateInterface = gameStateInterface;
        this.coopFinalizer = coopFinalizer;

        loadingInterface.HideLoadingScreen();

        messageBroker.Subscribe<CharacterCreationFinished>(Handle_CharacterCreationFinished);
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<NetworkHeroRecieved>(Handle_NetworkHeroRecieved);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<CharacterCreationFinished>(Handle_CharacterCreationFinished);
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Unsubscribe<NetworkHeroRecieved>(Handle_NetworkHeroRecieved);
    }

    internal void Handle_CharacterCreationFinished(MessagePayload<CharacterCreationFinished> obj)
    {
        // Cover the client's own (character-creation) world with a loading screen until the
        // server campaign is ready, so the local world isn't briefly visible while we join.
        loadingInterface.ShowLoadingScreen(
            "Joining Coop Campaign",
            "Sending your character to the host...");

        registryManager.RegisterAllGameObjects();

        var playerId = controllerIdProvider.ControllerId;
        var data = heroInterface.PackageMainHero();

        // Clear all registries so next time the game is loaded, it re-registers loaded save objects
        registryManager.ClearAllRegistries();

        network.SendAll(new NetworkTransferNewHero(playerId, data));
    }

    internal void Handle_NetworkHeroRecieved(MessagePayload<NetworkHeroRecieved> obj)
    {
        Logic.Player = obj.What.Player;

        Logic.LoadSavedData();
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        coopFinalizer.Finalize("Client has been stopped");

        Logic.SetState<MainMenuState>();
    }

    public override void EnterMainMenu()
    {
        gameStateInterface.GoToMainMenu();
    }

    public override void Connect()
    {
    }

    public override void Disconnect()
    {
        gameStateInterface.GoToMainMenu();
    }

    public override void ExitGame()
    {
    }

    public override void LoadSavedData()
    {
        Logic.SetState<ReceivingSavedDataState>();
    }

    public override void StartCharacterCreation()
    {
    }

    public override void EnterCampaignState()
    {
    }

    public override void EnterMissionState()
    {
    }

    public override void ValidateModules()
    {
    }
}
