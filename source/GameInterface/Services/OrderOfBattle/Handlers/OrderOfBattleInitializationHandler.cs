using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.OrderOfBattle.Data;
using GameInterface.Services.OrderOfBattle.Interfaces;
using GameInterface.Services.OrderOfBattle.Messages;

namespace GameInterface.Services.OrderOfBattle.Handlers;

internal class OrderOfBattleInitializationHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IOrderOfBattleCampaignBehaviorInterface orderOfBattleInterface;

    private OrderOfBattlePlayerData orderOfBattlePlayerData;

    public OrderOfBattleInitializationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IOrderOfBattleCampaignBehaviorInterface orderOfBattleInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.orderOfBattleInterface = orderOfBattleInterface;

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

    // Need to load the order of battle when the hero changes for the player
    private void Handle(MessagePayload<PlayerHeroChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.NewHero, out string playerHeroId)) return;

        // Null check for players without an existing order of battle
        OrderOfBattleData orderOfBattle = null;
        orderOfBattlePlayerData?.PlayerOrderOfBattles?.TryGetValue(playerHeroId, out orderOfBattle);

        orderOfBattleInterface.LoadFormations(orderOfBattle);
    }
}
