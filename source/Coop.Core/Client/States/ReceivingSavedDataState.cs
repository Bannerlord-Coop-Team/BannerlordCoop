using Common.Messaging;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.MobileParties.Messages;
using GameInterface.Services.GameState.Messages;
using LiteNetLib;

namespace Coop.Core.Client.States;

/// <summary>
/// State Logic Controller for the Receiving Saved Data State
/// </summary>
public class ReceivingSavedDataState : ClientStateBase
{
    private NetworkGameSaveDataReceived saveDataMessage = default;
    public ReceivingSavedDataState(IClientLogic logic) : base(logic)
    {
        Logic.MessageBroker.Subscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
        Logic.MessageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        Logic.MessageBroker.Subscribe<NetworkNewPartyCreated>(Handle_NetworkNewPartyCreated);
    }

    public override void Dispose()
    {
        Logic.MessageBroker.Unsubscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
        Logic.MessageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        Logic.MessageBroker.Unsubscribe<NetworkNewPartyCreated>(Handle_NetworkNewPartyCreated);
    }

    internal void Handle_NetworkGameSaveDataReceived(MessagePayload<NetworkGameSaveDataReceived> obj)
    {
        // TODO existing party does not switch correctly
        saveDataMessage = obj.What;
        Logic.EnterMainMenu();
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        var saveData = saveDataMessage?.GameSaveData;

        if (saveData == null) return;
        if (saveData.Length == 0) return;

        var commandLoad = new LoadGameSave(saveData);
        Logic.MessageBroker.Publish(this, commandLoad);

        Logic.LoadSavedData();
    }

    private void Handle_NetworkNewPartyCreated(MessagePayload<NetworkNewPartyCreated> obj)
    {
        var peer = (NetPeer)obj.Who;
        Logic.DeferredHeroRepository.AddDeferredHero(peer, obj.What.PlayerId, obj.What.PlayerHero);
    }

    public override void EnterMainMenu()
    {
        Logic.MessageBroker.Publish(this, new EnterMainMenu());
    }

    public override void Connect()
    {
    }

    public override void Disconnect()
    {
        Logic.MessageBroker.Publish(this, new EnterMainMenu());
        Logic.State = new MainMenuState(Logic);
    }

    public override void ExitGame()
    {
    }

    public override void LoadSavedData()
    {
        Logic.State = new LoadingState(Logic);
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
