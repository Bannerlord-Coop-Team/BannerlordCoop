// Ignore Spelling: Finalizer

using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Data;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Common;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.UI.Interfaces;
using Serilog;

namespace Coop.Core.Client.States;

/// <summary>
/// State controller for campaign client state
/// </summary>
public class CampaignState : ClientStateBase
{
    private readonly static ILogger Logger = LogManager.GetLogger<CampaignState>();

    private readonly IObjectManager objectManager;
    private readonly IPlayerRegistry playerRegistry;
    private readonly IDeferredHeroRepository deferredHeroRepository;
    private readonly IMessageBroker messageBroker;
    private readonly IHeroInterface heroInterface;
    private readonly ICoopFinalizer coopFinalizer;

    public CampaignState(
        IClientLogic logic,
        IObjectManager objectManager,
        IPlayerRegistry playerRegistry,
        IDeferredHeroRepository deferredHeroRepository,
        IMessageBroker messageBroker, 
        INetwork network,
        ILoadingInterface loadingInterface,
        IHeroInterface heroInterface,
        ICoopFinalizer coopFinalizer) : base(logic)
    {
        this.objectManager = objectManager;
        this.playerRegistry = playerRegistry;
        this.deferredHeroRepository = deferredHeroRepository;
        this.messageBroker = messageBroker;
        this.heroInterface = heroInterface;
        this.coopFinalizer = coopFinalizer;

        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<MissionStateEntered>(Handle_MissionStateEntered);
        messageBroker.Subscribe<NetworkNewPlayerHeroCreated>(Handle_NetworkNewPlayerHeroCreated);

        loadingInterface.SetLoadingMessage(
            "Loading Host Campaign",
            "Creating remote player heroes...");
        InstantiateDeferredHeroes();

        network.SendAll(new NetworkPlayerCampaignEntered());

        loadingInterface.HideLoadingScreen();
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Unsubscribe<MissionStateEntered>(Handle_MissionStateEntered);
        messageBroker.Unsubscribe<NetworkNewPlayerHeroCreated>(Handle_NetworkNewPlayerHeroCreated);
    }

    internal void Handle_MissionStateEntered(MessagePayload<MissionStateEntered> obj)
    {
        Logic.SetState<MissionState>();
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        coopFinalizer.Finalize("Client has been stopped");
    }

    private void Handle_NetworkNewPlayerHeroCreated(MessagePayload<NetworkNewPlayerHeroCreated> payload)
    {
        var obj = payload.What;
        CreatePlayerHero(obj.ControllerId, obj.Player, obj.HeroData);
    }
    
    private void InstantiateDeferredHeroes()
    {
        foreach (var message in deferredHeroRepository.GetAllDeferredHeroes())
        {
            CreatePlayerHero(message.ControllerId, message.Player, message.HeroData);
        }

        deferredHeroRepository.Clear();
    }

    private void CreatePlayerHero(string controllerId, Player player, byte[] data)
    {
        heroInterface.UnpackHero(controllerId, data);

        if (!playerRegistry.AddPlayer(player))
            Logger.Error("Player has been already added.");
    }
         
    public override void EnterMissionState()
    {
        messageBroker.Publish(this, new EnterMissionState());
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
    }

    public override void StartCharacterCreation()
    {
    }

    public override void EnterCampaignState()
    {
    }

    public override void ValidateModules()
    {
    }
}
