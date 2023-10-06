// Ignore Spelling: Finalizer

using Common.Messaging;
using Common.Network;
using Coop.Core.Common;
using Coop.Core.Common.Services.Connection.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Entity.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Client.States;

/// <summary>
/// State controller for the character creation client state
/// </summary>
public class CharacterCreationState : ClientStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly ICoopFinalizer coopFinalizer;

    public CharacterCreationState(
        IClientLogic logic,
        IMessageBroker messageBroker,
        INetwork network, 
        IControllerIdProvider controllerIdProvider,
        ICoopFinalizer coopFinalizer) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.controllerIdProvider = controllerIdProvider;
        this.coopFinalizer = coopFinalizer;
        messageBroker.Subscribe<NewHeroPackaged>(Handle_NewHeroPackaged);
        messageBroker.Subscribe<CharacterCreationFinished>(Handle_CharacterCreationFinished);
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<NetworkPlayerData>(Handle_NetworkPlayerData);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NewHeroPackaged>(Handle_NewHeroPackaged);
        messageBroker.Unsubscribe<CharacterCreationFinished>(Handle_CharacterCreationFinished);
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Unsubscribe<NetworkPlayerData>(Handle_NetworkPlayerData);
    }

    internal void Handle_CharacterCreationFinished(MessagePayload<CharacterCreationFinished> obj)
    {
        messageBroker.Publish(this, new PackageMainHero());
    }

    internal void Handle_NewHeroPackaged(MessagePayload<NewHeroPackaged> obj)
    {
        var playerId = controllerIdProvider.ControllerId;
        var data = obj.What.Package;

        network.SendAll(new NetworkTransferedHero(playerId, data));
    }

    internal void Handle_NetworkPlayerData(MessagePayload<NetworkPlayerData> obj)
    {
        Logic.ControlledHeroId = obj.What.HeroStringId;

        var controllerId = controllerIdProvider.ControllerId;

        messageBroker.Publish(this, new AddControlledEntity(controllerId, obj.What.HeroStringId));
        messageBroker.Publish(this, new AddControlledEntity(controllerId, obj.What.PartyStringId));

        Logic.LoadSavedData();
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        coopFinalizer.Finalize("Client has been stopped");
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
