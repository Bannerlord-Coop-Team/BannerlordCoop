using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Heroes.Handlers;

internal class HeroCreatorHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroCreatorHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public HeroCreatorHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<InitializeNewHero>(Handle_InitializeNewHero);
        messageBroker.Subscribe<NetworkInitializeNewHero>(Handle_NetworkInitializeNewHero);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InitializeNewHero>(Handle_InitializeNewHero);
        messageBroker.Unsubscribe<NetworkInitializeNewHero>(Handle_NetworkInitializeNewHero);
    }

    private void Handle_InitializeNewHero(MessagePayload<InitializeNewHero> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Hero.BattleEquipment, out var battleEquipmentId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Hero.CivilianEquipment, out var civilianEquipmentId)) return;

        var message = new NetworkInitializeNewHero(
            heroId,
            data.Hero.FirstName,
            data.Hero.Name,
            battleEquipmentId,
            civilianEquipmentId,
            data.Hero.CivilianEquipment,
            data.Hero.BattleEquipment);
        network.SendAll(message);
    }

    private void Handle_NetworkInitializeNewHero(MessagePayload<NetworkInitializeNewHero> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero)) return;
            if (!objectManager.TryGetObjectWithLogging<Equipment>(data.BattleEquipmentId, out var battleEquipment)) return;
            if (!objectManager.TryGetObjectWithLogging<Equipment>(data.CivilianEquipmentId, out var civilianEquipment)) return;

            using (new AllowedThread())
            {
                hero.SetName(data.Name, data.FirstName);
                battleEquipment = data.BattleEquipment;
                civilianEquipment = data.CivilianEquipment;
                hero._battleEquipment = battleEquipment;
                hero._civilianEquipment = civilianEquipment;
            }
        });
    }
}
