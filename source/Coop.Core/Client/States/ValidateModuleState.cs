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

    // Teardown can be started from the game thread (validation-timeout timer) and the poller thread
    // (a denied or late validation response) at the same instant. This latch, claimed via Interlocked,
    // runs the teardown exactly once. Dispose claims it too, so a timeout firing as the state cleanly
    // transitions no-ops instead of tearing the new state down.
    private int teardownClaimed;
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
        // state transition. RunSafe (not Run) so a throw during teardown is logged instead of
        // escaping into the game-loop pump and killing that frame's queue drain.
        validationTimeoutTimer = new Timer(
            _ => GameThread.RunSafe(TimeoutValidation), null, ValidationTimeout, Timeout.InfiniteTimeSpan);
    }

    public override void Dispose()
    {
        disposed = true;
        // Claim the teardown latch so an in-flight timeout callback that already passed its guard
        // finds teardown handled and no-ops (see TimeoutValidation / Disconnect).
        Interlocked.Exchange(ref teardownClaimed, 1);
        validationTimeoutTimer?.Dispose();
        messageBroker.Unsubscribe<NetworkModuleVersionsValidated>(Handle_NetworkModuleVersionsValidated);
        messageBroker.Unsubscribe<NetworkClientValidated>(Handle_NetworkClientValidated);
        messageBroker.Unsubscribe<CharacterCreationStarted>(Handle_CharacterCreationStarted);
    }

    internal void TimeoutValidation()
    {
        // Marshaled onto the game thread by the timer callback. The guard skips the spurious error log
        // when the state was already left; correctness does not depend on it being atomic, because the
        // teardown below goes through THIS state's Disconnect (never Logic.Disconnect, which would hit
        // whatever state replaced it), and Disconnect's Interlocked latch — also claimed by Dispose —
        // makes a denied/late response landing at the same instant tear down exactly once.
        if (disposed || Logic.State != this) return;

        Logger.Error(
            "Timed out after {Timeout}s waiting for the server to validate the connection",
            ValidationTimeout.TotalSeconds);

        disconnectReason =
            "Timed out waiting for the server to validate the connection.\n" +
            "The server may be running an incompatible version of the mod.";
        Disconnect();
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
        // Teardown can be initiated from the game thread (validation-timeout timer) and the poller
        // thread (a denied or late validation response) at the same instant; the latch runs
        // CoopFinalizer exactly once.
        if (Interlocked.Exchange(ref teardownClaimed, 1) != 0) return;

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
