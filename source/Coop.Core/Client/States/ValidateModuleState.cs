using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameDebug.Interfaces;
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
    public ValidateModuleState(IClientLogic logic) : base(logic)
    {
        messageBroker = logic.MessageBroker;
        network = logic.Network;

        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<CharacterCreationStarted>(Handle_CharacterCreationStarted);
        messageBroker.Subscribe<NetworkClientValidated>(Handle_NetworkClientValidated);

        network.SendAll(new NetworkClientValidate(DebugHeroInterface.Player1_Id));
    }

    public override void Dispose()
    {
        Logic.MessageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        Logic.MessageBroker.Unsubscribe<CharacterCreationStarted>(Handle_CharacterCreationStarted);
        Logic.MessageBroker.Unsubscribe<NetworkClientValidated>(Handle_NetworkClientValidated);
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
        Logic.State = new CharacterCreationState(Logic);
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        Logic.State = new MainMenuState(Logic);
    }

    public override void EnterMainMenu()
    {
        Logic.MessageBroker.Publish(this, new EnterMainMenu());
    }

    public override void LoadSavedData()
    {
        Logic.State = new ReceivingSavedDataState(Logic);
    }

    public override void Connect()
    {
    }

    public override void Disconnect()
    {
        Logic.MessageBroker.Publish(this, new EnterMainMenu());
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
        Logic.MessageBroker.Publish(this, new StartCharacterCreation());
    }

    public override void ValidateModules()
    {
    }
}
