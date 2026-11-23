using Common.Messaging;
using Coop.Naval.CampaignBehaviors.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.OrderOfBattle;
using GameInterface.Services.OrderOfBattle.Data;
using GameInterface.Services.OrderOfBattle.Messages;

namespace Coop.Naval.CampaignBehaviors.Handlers;

internal class NavalOrderOfBattleInitializationHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INavalOrderOfBattleCampaignBehaviorInterface navalOrderOfBattleInterface;

    private OrderOfBattlePlayerData orderOfBattlePlayerData;

    public NavalOrderOfBattleInitializationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INavalOrderOfBattleCampaignBehaviorInterface navalOrderOfBattleInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.navalOrderOfBattleInterface = navalOrderOfBattleInterface;

        messageBroker.Subscribe<InitializeClientOrderOfBattleData>(Handle);
        messageBroker.Subscribe<PlayerHeroChanged>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InitializeClientOrderOfBattleData>(Handle);
        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle);
    }

    private void Handle(MessagePayload<InitializeClientOrderOfBattleData> obj)
    {
        orderOfBattlePlayerData = obj.What.OrderOfBattlePlayerData;
    }

    // Need to load the naval order of battle when the hero changes for the player
    private void Handle(MessagePayload<PlayerHeroChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.NewHero, out string playerHeroId)) return;

        // Null check for players without an existing order of battle
        OrderOfBattleData orderOfBattle = null;
        orderOfBattlePlayerData?.PlayerOrderOfBattles?.TryGetValue(playerHeroId, out orderOfBattle);

        navalOrderOfBattleInterface.LoadFormations(orderOfBattle);
    }
}
