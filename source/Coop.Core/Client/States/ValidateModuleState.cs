using System;
using Common.Messaging;
using Common.Network;
using Coop.Core.Common;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Modules;

namespace Coop.Core.Client.States;

/// <summary>
/// State Logic Controller for the Validate Module Client State
/// </summary>
public class ValidateModuleState : ClientStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly ICoopFinalizer coopFinalizer;
    private DateTime? ignoreMainMenuUntilUtc;

    public ValidateModuleState(
        IClientLogic logic,
        IMessageBroker messageBroker,
        INetwork network,
        IControllerIdProvider controllerIdProvider,
        ICoopFinalizer coopFinalizer,
        IModuleInfoProvider moduleInfoProvider) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.controllerIdProvider = controllerIdProvider;
        this.coopFinalizer = coopFinalizer;
        // During connection, subscribe to key events to drive validation and branch to next states.
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<NetworkModuleVersionsValidated>(Handle_NetworkModuleVersionsValidated);
        messageBroker.Subscribe<NetworkClientValidated>(Handle_NetworkClientValidated);
        messageBroker.Subscribe<CharacterCreationStarted>(Handle_CharacterCreationStarted);

        // Temporary gate to ignore menu re-entry while validation is in progress.
        ignoreMainMenuUntilUtc = DateTime.UtcNow.AddMinutes(2);

#if DEBUG
        controllerIdProvider.SetControllerFromProgramArgs();
#else
        controllerIdProvider.SetControllerAsPlatformId();
#endif

        // Kick off module validation against the server to ensure binary compatibility.
        network.SendAll(new NetworkModuleVersionsValidate(moduleInfoProvider.GetModuleInfos()));
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Unsubscribe<NetworkModuleVersionsValidated>(Handle_NetworkModuleVersionsValidated);
        messageBroker.Unsubscribe<NetworkClientValidated>(Handle_NetworkClientValidated);
        messageBroker.Unsubscribe<CharacterCreationStarted>(Handle_CharacterCreationStarted);
    }

    internal void Handle_NetworkModuleVersionsValidated(MessagePayload<NetworkModuleVersionsValidated> obj)
    {
        if (obj.What.Matches)
        {
            ignoreMainMenuUntilUtc = null;
            messageBroker.Publish(this, new SendInformationMessage("Modules validés"));
            // Proceed to client validation (associate this controller with a hero).
            network.SendAll(new NetworkClientValidate(controllerIdProvider.ControllerId));
        }
        else
        {
            messageBroker.Publish(this, new SendInformationMessage("Module validation failed!\nReason: " + obj.What.Reason));
            Logic.Disconnect();
        }
    }

    internal void Handle_NetworkClientValidated(MessagePayload<NetworkClientValidated> obj)
    {
        if (obj.What.HeroExists)
        {
            ignoreMainMenuUntilUtc = null;
            messageBroker.Publish(this, new SendInformationMessage($"Héros reconnu: {obj.What.HeroId}"));
            Logic.ControlledHeroId = obj.What.HeroId;
            // Existing hero found; begin save-transfer and load path.
            Logic.LoadSavedData();
        }
        else
        {
            ignoreMainMenuUntilUtc = null;
            messageBroker.Publish(this, new SendInformationMessage("Démarrage de la création de personnage"));
            // No hero found; start the character creation flow.
            Logic.StartCharacterCreation();   
        }
    }

    internal void Handle_CharacterCreationStarted(MessagePayload<CharacterCreationStarted> obj)
    {
        // Transition to CharacterCreationState once Bannerlord signals the character creation state has activated.
        Logic.SetState<CharacterCreationState>();
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        // En phase de connexion/validation, ignorer les notifications d'entrée du menu principal
        // pour éviter un retour prématuré au menu.
        return;
    }

    public override void EnterMainMenu()
    {
        messageBroker.Publish(this, new EnterMainMenu());
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
        messageBroker.Publish(this, new EnterMainMenu());
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
        // Publish the command that triggers GameInterface to start a new SandBox game, entering character creation.
        messageBroker.Publish(this, new StartCharacterCreation());
    }

    public override void ValidateModules()
    {
    }
}
