// Ignore Spelling: Finalizer

using Common.Messaging;
using Common.Network;
using Coop.Core.Common;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Registry;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Players.Data;
using GameInterface.Services.UI.Messages;
using NetworkPlayerData = Coop.Core.Server.Connections.Messages.NetworkPlayerData;

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
    private readonly IControlledEntityRegistry controlledEntityRegistry;
    private readonly ICoopFinalizer coopFinalizer;

    public CharacterCreationState(
        IClientLogic logic,
        IMessageBroker messageBroker,
        INetwork network,
        IHeroInterface heroInterface,
        IRegistryManager registryManager,
        IControllerIdProvider controllerIdProvider,
        IControlledEntityRegistry controlledEntityRegistry,
        ICoopFinalizer coopFinalizer) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.heroInterface = heroInterface;
        this.registryManager = registryManager;
        this.controllerIdProvider = controllerIdProvider;
        this.controlledEntityRegistry = controlledEntityRegistry;
        this.coopFinalizer = coopFinalizer;
        messageBroker.Subscribe<CharacterCreationFinished>(Handle_CharacterCreationFinished);
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<NetworkPlayerData>(Handle_NetworkPlayerData);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<CharacterCreationFinished>(Handle_CharacterCreationFinished);
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Unsubscribe<NetworkPlayerData>(Handle_NetworkPlayerData);
    }

    internal void Handle_CharacterCreationFinished(MessagePayload<CharacterCreationFinished> obj)
    {
        // Cover the client's own (character-creation) world with a loading screen until the
        // server campaign is ready, so the local world isn't briefly visible while we join.
        messageBroker.Publish(this, new StartLoadingScreen());

        registryManager.RegisterAllGameObjects();

        var playerId = controllerIdProvider.ControllerId;
        var data = heroInterface.PackageMainHero();

        // Clear all registries so next time the game is loaded, it re-registers loaded save objects
        registryManager.ClearAllRegistries();

        network.SendAll(new NetworkTransferedHero(playerId, data));
    }

    internal void Handle_NetworkPlayerData(MessagePayload<NetworkPlayerData> obj)
    {
        Logic.Player = new Player(obj.What.HeroStringId, obj.What.PartyStringId);

        var controllerId = controllerIdProvider.ControllerId;

        controlledEntityRegistry.RegisterAsControlled(controllerId, obj.What.HeroStringId);
        controlledEntityRegistry.RegisterAsControlled(controllerId, obj.What.PartyStringId);

        Logic.LoadSavedData();
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        coopFinalizer.Finalize("Client has been stopped");

        Logic.SetState<MainMenuState>();
    }

    public override void EnterMainMenu()
    {
        messageBroker.Publish(this, new EnterMainMenu());
    }

    public override void Connect()
    {
    }

    public override void Disconnect()
    {
        messageBroker.Publish(this, new EnterMainMenu());
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
