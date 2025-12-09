using Common.Messaging;
using Coop.Core.Client.Services.Heroes.Data;
using Coop.Core.Common;
using GameInterface.Registry.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;
using System;
using GameInterface.Services.GameDebug.Messages;

namespace Coop.Core.Client.States;

/// <summary>
/// State Logic Controller for the Loading Client State
/// </summary>
public class LoadingState : ClientStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly IDeferredHeroRepository deferredHeroRepo;

    public LoadingState(
        IClientLogic logic,
        IMessageBroker messageBroker,
        IDeferredHeroRepository deferredHeroRepo) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.deferredHeroRepo = deferredHeroRepo;
        messageBroker.Subscribe<CampaignReady>(Handle_CampaignLoaded);
        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
        messageBroker.Subscribe<GameSaveLoaded>(Handle_GameSaveLoaded);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<CampaignReady>(Handle_CampaignLoaded);
        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
        messageBroker.Unsubscribe<GameSaveLoaded>(Handle_GameSaveLoaded);
    }

    public override void EnterMainMenu()
    {
        messageBroker.Publish(this, new EnterMainMenu());
    }

    internal void Handle_CampaignLoaded(MessagePayload<CampaignReady> obj)
    {
        messageBroker.Publish(this, new SendInformationMessage("Campagne prête, enregistrement des objets"));
        messageBroker.Publish(this, new RegisterAllGameObjects());
    }

    internal void Handle_GameSaveLoaded(MessagePayload<GameSaveLoaded> obj)
    {
        messageBroker.Publish(this, new SendInformationMessage("Sauvegarde chargée, enregistrement des objets"));
        messageBroker.Publish(this, new RegisterAllGameObjects());
    }

    internal void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> obj)
    {
        InstantiateDeferredHeroes();

        if (!string.IsNullOrEmpty(Logic.ControlledHeroId))
        {
            messageBroker.Publish(this, new SendInformationMessage($"Bascule sur héros {Logic.ControlledHeroId}"));
            messageBroker.Publish(this, new SwitchToHero(Logic.ControlledHeroId));
        }
        else
        {
            messageBroker.Publish(this, new SendInformationMessage("Aucun héros spécifié, entrée campagne sans bascule"));
        }

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
