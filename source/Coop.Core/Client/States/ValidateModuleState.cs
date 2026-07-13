using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Common;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.Modules;
using Serilog;
using System;
using System.Threading;

namespace Coop.Core.Client.States;

/// <summary>
/// State Logic Controller for the Validate Module Client State
/// </summary>
public class ValidateModuleState : ClientStateBase
{
    private static readonly ILogger Logger = LogManager.GetLogger<ValidateModuleState>();

    /// <summary>
    /// How long the client waits for the server's validation responses before giving up. The
    /// exchange is two immediate request/response roundtrips, so anything beyond this means the
    /// server never answered (validation crashed server-side, or the builds are so different the
    /// request could not even be deserialized) — without a deadline the player would sit on the
    /// "Validating modules..." loading screen forever.
    /// </summary>
    internal static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(30);

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly ICoopFinalizer coopFinalizer;
    private readonly IGameStateInterface gameStateInterface;
    private readonly Timer validationTimeoutTimer;

    private volatile bool disposed;
    private string disconnectReason;

    public ValidateModuleState(
        IClientLogic logic,
        IMessageBroker messageBroker,
        INetwork network,
        IControllerIdProvider controllerIdProvider,
        ICoopFinalizer coopFinalizer,
        IGameStateInterface gameStateInterface,
        IModuleInfoProvider moduleInfoProvider) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.controllerIdProvider = controllerIdProvider;
        this.coopFinalizer = coopFinalizer;
        this.gameStateInterface = gameStateInterface;
        messageBroker.Subscribe<NetworkModuleVersionsValidated>(Handle_NetworkModuleVersionsValidated);
        messageBroker.Subscribe<NetworkClientValidated>(Handle_NetworkClientValidated);
        messageBroker.Subscribe<CharacterCreationStarted>(Handle_CharacterCreationStarted);

#if DEBUG
        controllerIdProvider.SetControllerFromProgramArgs();
#else
        controllerIdProvider.SetControllerAsPlatformId();
#endif

        network.SendAll(new NetworkModuleVersionsValidate(moduleInfoProvider.GetModuleInfos()));

        // One-shot deadline covering this state's whole exchange; leaving the state disposes it.
        // The timer thread only marshals — the decision runs on the game thread like every other
        // state transition.
        validationTimeoutTimer = new Timer(
            _ => GameThread.Run(TimeoutValidation), null, ValidationTimeout, Timeout.InfiniteTimeSpan);
    }

    public override void Dispose()
    {
        disposed = true;
        validationTimeoutTimer?.Dispose();
        messageBroker.Unsubscribe<NetworkModuleVersionsValidated>(Handle_NetworkModuleVersionsValidated);
        messageBroker.Unsubscribe<NetworkClientValidated>(Handle_NetworkClientValidated);
        messageBroker.Unsubscribe<CharacterCreationStarted>(Handle_CharacterCreationStarted);
    }

    internal void TimeoutValidation()
    {
        // The state may have been left (Dispose) or coop torn down between the timer firing and
        // this running on the game thread.
        if (disposed || Logic.State != this) return;

        Logger.Error(
            "Timed out after {Timeout}s waiting for the server to validate the connection",
            ValidationTimeout.TotalSeconds);

        disconnectReason =
            "Timed out waiting for the server to validate the connection.\n" +
            "The server may be running an incompatible version of the mod.";
        Logic.Disconnect();
    }

    internal void Handle_NetworkModuleVersionsValidated(MessagePayload<NetworkModuleVersionsValidated> obj)
    {
        if (obj.What.Matches)
        {
            network.SendAll(new NetworkClientValidate(controllerIdProvider.ControllerId));
        }
        else
        {
            var reason = "Module validation failed!\nReason: " + obj.What.Reason;
            messageBroker.Publish(this, new SendInformationMessage(reason));

            // Carry the reason into the teardown pop-up: the information message above lands in the
            // chat log, which is invisible behind the forced loading screen the player is watching.
            disconnectReason = reason;
            Logic.Disconnect();
        }
    }

    internal void Handle_NetworkClientValidated(MessagePayload<NetworkClientValidated> obj)
    {
        if (obj.What.HeroExists)
        {
            Logic.Player = obj.What.Player;
            Logic.LoadSavedData();
        }
        else
        {
            Logic.StartCharacterCreation();   
        }
    }

    internal void Handle_CharacterCreationStarted(MessagePayload<CharacterCreationStarted> obj)
    {
        Logic.SetState<CharacterCreationState>();
    }

    public override void EnterMainMenu()
    {
    }

    public override void LoadSavedData()
    {
        Logic.SetState<ReceivingSavedDataState>();
    }

    public override void Connect()
    {
    }

    public override void Disconnect()
    {
        validationTimeoutTimer?.Dispose();

        // Finalize tears down coop (EndCoopMode -> DestroyContainer), which disposes the container the
        // state machine resolves from — so do NOT SetState afterwards (it would resolve from a disposed
        // container and throw). This matches the teardown in the other states (e.g. CampaignState).
        coopFinalizer.Finalize(disconnectReason ?? "Client has been stopped");
    }

    public override void EnterCampaignState()
    {
    }

    public override void EnterMissionState()
    {
    }

    public override void ExitGame()
    {
    }

    public override void StartCharacterCreation()
    {
        messageBroker.Publish(this, new StartCharacterCreation());
    }

    public override void ValidateModules()
    {
    }
}
