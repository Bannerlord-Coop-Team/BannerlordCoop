// Ignore Spelling: Finalizer

using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Common;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Registry;
using Common;
using GameInterface.Services.AdvancedStart.Data;
using GameInterface.Services.AdvancedStart.Interfaces;
using System;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.UI.Interfaces;
using Serilog;

namespace Coop.Core.Client.States;

/// <summary>
/// State controller for the character creation client state
/// </summary>
public class CharacterCreationState : ClientStateBase
{
    private static readonly ILogger Logger = LogManager.GetLogger<CharacterCreationState>();
    private const string JoiningTitle = "Joining Coop Campaign";
    private const string JoiningText = "Sending your character to the host...";

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IHeroInterface heroInterface;
    private readonly IRegistryManager registryManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly ILoadingInterface loadingInterface;
    private readonly IPlayerManager playerManager;
    private readonly IGameStateInterface gameStateInterface;
    private readonly ICoopFinalizer coopFinalizer;
    private readonly IAdvancedStartInterface advancedStartInterface;

    private volatile bool disposed;
    // Game thread only: the server's policy has arrived, so the window can open as soon as creation finishes.
    private bool policyReceived;
    // Game thread only: creation finished before the policy arrived, so its arrival opens the window.
    private bool awaitingPolicy;

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
        ICoopFinalizer coopFinalizer,
        IAdvancedStartInterface advancedStartInterface) : base(logic)
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
        this.advancedStartInterface = advancedStartInterface;

        loadingInterface.HideLoadingScreen();

        messageBroker.Subscribe<CharacterCreationFinished>(Handle_CharacterCreationFinished);
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<NetworkHeroRecieved>(Handle_NetworkHeroRecieved);
        messageBroker.Subscribe<NetworkAdvancedStartPolicy>(Handle_NetworkAdvancedStartPolicy);

        // Asked up front so the review stage can label its button for what comes next. The server pushes a new
        // policy whenever it changes, so this one stays current until the window opens.
        network.SendAll(new NetworkRequestAdvancedStartPolicy());
    }

    public override void Dispose()
    {
        disposed = true;
        messageBroker.Unsubscribe<CharacterCreationFinished>(Handle_CharacterCreationFinished);
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Unsubscribe<NetworkHeroRecieved>(Handle_NetworkHeroRecieved);
        messageBroker.Unsubscribe<NetworkAdvancedStartPolicy>(Handle_NetworkAdvancedStartPolicy);

        // A disconnect while the player is picking a start must not leave the window open over the main menu.
        GameThread.RunSafe(advancedStartInterface.EndJoin, context: nameof(Dispose));
    }

    internal void Handle_CharacterCreationFinished(MessagePayload<CharacterCreationFinished> obj)
    {
        // Cover the client's own (character-creation) world with a loading screen until the
        // server campaign is ready, so the local world isn't briefly visible while we join.
        loadingInterface.ShowLoadingScreen(JoiningTitle, JoiningText);

        if (policyReceived)
            OpenStartOptions();
        else
            awaitingPolicy = true;
    }

    internal void Handle_NetworkAdvancedStartPolicy(MessagePayload<NetworkAdvancedStartPolicy> obj)
    {
        var policy = obj.What.Policy;

        GameThread.RunSafe(() =>
        {
            if (disposed || Logic.State != this) return;

            policyReceived = true;
            advancedStartInterface.UpdateServerPolicy(policy);

            if (!awaitingPolicy) return;
            awaitingPolicy = false;
            OpenStartOptions();
        }, context: nameof(Handle_NetworkAdvancedStartPolicy));
    }

    private void OpenStartOptions()
    {
        bool shown;
        try
        {
            shown = advancedStartInterface.TryShowStartOptions(SendHero, Logic.EnterMainMenu);
        }
        catch (Exception exception)
        {
            // Without a window nothing would ever send the hero, so join with the default start instead.
            Logger.Error(exception, "Failed to open the starting options window; joining with the default start");
            shown = false;
        }

        if (!shown)
            SendHero(Array.Empty<AdvancedStartSelection>());
    }

    private void SendHero(AdvancedStartSelection[] advancedStart)
    {
        loadingInterface.ShowLoadingScreen(JoiningTitle, JoiningText);

        registryManager.RegisterAllGameObjects();

        var playerId = controllerIdProvider.ControllerId;
        var data = heroInterface.PackageMainHero();

        // Clear all registries so next time the game is loaded, it re-registers loaded save objects
        registryManager.ClearAllRegistries();

        network.SendAll(new NetworkTransferNewHero(playerId, data, advancedStart));
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
