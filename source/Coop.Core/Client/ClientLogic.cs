using Common.Logging;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.States;
using GameInterface.Services.Entity;
using HarmonyLib;
using Serilog;

namespace Coop.Core.Client;

/// <summary>
/// Top level client-side state machine logic orchestrator
/// </summary>
public interface IClientLogic : ILogic, IClientState
{
    /// <summary>
    /// Client-side state
    /// </summary>
    IClientState State { get; set; }

    /// <summary>
    /// Networking Client for Client-side
    /// </summary>
    INetwork Network { get; }
    IMessageBroker MessageBroker { get; }
    string ControlledHeroId { get; set; }

    IControllerIdProvider ControllerIdProvider { get; }
}

/// <inheritdoc cref="IClientLogic"/>
public class ClientLogic : IClientLogic
{
    private readonly ILogger Logger = LogManager.GetLogger<ClientLogic>();
    public INetwork Network { get; }
    public IMessageBroker MessageBroker { get; }
    public IControllerIdProvider ControllerIdProvider { get; }
    public string ControlledHeroId { get; set; }

    private readonly Harmony harmony = new Harmony("com.TaleWorlds.MountAndBlade.Bannerlord.Coop");
    public IClientState State 
    {
        get { return _state; }
        set 
        {
            Logger.Debug("Client is changing to {state} State", value.GetType().Name);

            _state?.Dispose();
            _state = value;
        } 
    }

    

    private IClientState _state;

    public ClientLogic(
        INetwork network,
        IMessageBroker messageBroker,
        IControllerIdProvider controllerIdProvider)
    {
        Network = network;
        MessageBroker = messageBroker;
        ControllerIdProvider = controllerIdProvider;
        State = new MainMenuState(this);

        // Apply all patches via harmony
        harmony.PatchAll(typeof(GameInterface.GameInterface).Assembly);
    }

    public void Start()
    {
        Connect();
    }

    public void Stop()
    {
        Disconnect();
    }

    public void Dispose()
    {
        harmony.UnpatchAll();
        State.Dispose();
    }

    public void Connect() => State.Connect();

    public void Disconnect() => State.Disconnect();

    public void StartCharacterCreation() => State.StartCharacterCreation();

    public void LoadSavedData() => State.LoadSavedData();

    public void ExitGame() => State.ExitGame();

    public void EnterMainMenu() => State.EnterMainMenu();

    public void EnterCampaignState() => State.EnterCampaignState();

    public void EnterMissionState() => State.EnterMissionState();

    public void ValidateModules() => State.ValidateModules();
}
