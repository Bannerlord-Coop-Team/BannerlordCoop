using Common.Messaging;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Heroes.Data;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Common;
using GameInterface.Services.GameState.Messages;
using LiteNetLib;

namespace Coop.Core.Client.States;

/// <summary>
/// State Logic Controller for the Receiving Saved Data State
/// </summary>
public class ReceivingSavedDataState : ClientStateBase
{
    private NetworkGameSaveDataReceived saveDataMessage = default;
    private readonly IMessageBroker messageBroker;
    private readonly IDeferredHeroRepository deferredHeroRepo;
    private readonly ICoopFinalizer coopFinalizer;

    public ReceivingSavedDataState(
        IClientLogic logic,
        IMessageBroker messageBroker,
        IDeferredHeroRepository deferredHeroRepo,
        ICoopFinalizer coopFinalizer) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.deferredHeroRepo = deferredHeroRepo;
        this.coopFinalizer = coopFinalizer;
        messageBroker.Subscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<NetworkNewPartyCreated>(Handle_NetworkNewPartyCreated);
        
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Unsubscribe<NetworkNewPartyCreated>(Handle_NetworkNewPartyCreated);
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
        messageBroker.Publish(this, commandLoad);

        Logic.LoadSavedData();
    }

    private void Handle_NetworkNewPartyCreated(MessagePayload<NetworkNewPartyCreated> obj)
    {
        var peer = (NetPeer)obj.Who;
        deferredHeroRepo.AddDeferredHero(peer, obj.What.PlayerId, obj.What.PlayerHero);
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

        Logic.SetState<MainMenuState>();
    }

    public override void ExitGame()
    {
    }

    public override void LoadSavedData()
    {
        Logic.SetState<LoadingState>();
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
