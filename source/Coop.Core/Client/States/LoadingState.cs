using Common.Messaging;
using Coop.Core.Client.Services.Heroes.Data;
using GameInterface;
using GameInterface.Registry;
using GameInterface.Services.Entity;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Client.States;

/// <summary>
/// State Logic Controller for the Loading Client State
/// </summary>
public class LoadingState : ClientStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly IRegistryManager registryManager;
    private readonly IDeferredHeroRepository deferredHeroRepo;
    private readonly IHeroInterface heroInterface;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IControlledEntityRegistry controlledEntityRegistry;

    public LoadingState(
        IClientLogic logic,
        IMessageBroker messageBroker,
        IRegistryManager registryManager,
        IDeferredHeroRepository deferredHeroRepo,
        IHeroInterface heroInterface,
        IControllerIdProvider controllerIdProvider,
        IControlledEntityRegistry controlledEntityRegistry) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.registryManager = registryManager;
        this.deferredHeroRepo = deferredHeroRepo;
        this.heroInterface = heroInterface;
        this.controllerIdProvider = controllerIdProvider;
        this.controlledEntityRegistry = controlledEntityRegistry;

        messageBroker.Subscribe<CampaignReady>(Handle_CampaignLoaded);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<CampaignReady>(Handle_CampaignLoaded);
    }

    public override void EnterMainMenu()
    {
        messageBroker.Publish(this, new EnterMainMenu());
    }

    internal void Handle_CampaignLoaded(MessagePayload<CampaignReady> obj)
    {
        registryManager.RegisterAllGameObjects();
        registryManager.PatchLifetimes();

        InstantiateDeferredHeroes();

        RegisterPlayerAsControlled();

        heroInterface.SwitchToPlayer(Logic.Player);

        Logic.EnterCampaignState();
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
        Logic.SetState<CampaignState>();
    }

    public override void EnterMissionState()
    {
    }

    public override void ValidateModules()
    {
    }

    /// <summary>
    /// Registers the client's own hero and party as controlled by this client so that
    /// the client owns its party's movement/AI once it switches to it. Mirrors the
    /// server registering all parties as controlled when its save loads.
    /// </summary>
    private void RegisterPlayerAsControlled()
    {
        var player = Logic.Player;
        if (player == null) return;

        var controllerId = controllerIdProvider.ControllerId;

        controlledEntityRegistry.RegisterAsControlled(controllerId, player.HeroId);
        controlledEntityRegistry.RegisterAsControlled(controllerId, player.MobilePartyId);
    }

    private void InstantiateDeferredHeroes()
    {
        foreach (var newHero in deferredHeroRepo.GetAllDeferredHeroes())
        {
            var message = new RegisterNewPlayerHero(newHero.NetPeer, newHero.ControllerId, newHero.HeroData);
            messageBroker.Publish(this, message);
        }

        deferredHeroRepo.Clear();
    }
}
