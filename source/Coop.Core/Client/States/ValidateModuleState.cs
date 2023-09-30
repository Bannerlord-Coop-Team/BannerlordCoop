using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States;

/// <summary>
/// State Logic Controller for the Validate Module Client State
/// </summary>
public class ValidateModuleState : ClientStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IControllerIdProvider controllerIdProvider;

    public ValidateModuleState(IClientLogic logic, IMessageBroker messageBroker, INetwork network, IControllerIdProvider controllerIdProvider) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<CharacterCreationStarted>(Handle_CharacterCreationStarted);
        messageBroker.Subscribe<NetworkClientValidated>(Handle_NetworkClientValidated);

#if DEBUG
        controllerIdProvider.SetControllerFromProgramArgs();
#else
        controllerIdProvider.SetControllerAsPlatformId();
#endif


        network.SendAll(new NetworkClientValidate(controllerIdProvider.ControllerId));
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Unsubscribe<CharacterCreationStarted>(Handle_CharacterCreationStarted);
        messageBroker.Unsubscribe<NetworkClientValidated>(Handle_NetworkClientValidated);
    }

    internal void Handle_NetworkClientValidated(MessagePayload<NetworkClientValidated> obj)
    {
        if (obj.What.HeroExists)
        {
            Logic.ControlledHeroId = obj.What.HeroId;
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

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        Logic.SetState<MainMenuState>();
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
        messageBroker.Publish(this, new StartCharacterCreation());
    }

    public override void ValidateModules()
    {
    }
}
