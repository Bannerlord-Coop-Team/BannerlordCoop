using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Companions.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Companions.Handlers;

internal class EquipmentAdjustmentHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<EquipmentAdjustmentHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public EquipmentAdjustmentHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<AdjustCompanionsEquipment>(Handle_AdjustCompanionsEquipment);
        messageBroker.Subscribe<NetworkAdjustCompanionsEquipment>(Handle_NetworkAdjustCompanionsEquipment);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AdjustCompanionsEquipment>(Handle_AdjustCompanionsEquipment);
        messageBroker.Unsubscribe<NetworkAdjustCompanionsEquipment>(Handle_NetworkAdjustCompanionsEquipment);
    }

    private void Handle_AdjustCompanionsEquipment(MessagePayload<AdjustCompanionsEquipment> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.CompanionHero, out var companionHeroId)) return;

        var message = new NetworkAdjustCompanionsEquipment(
            companionHeroId,
            data.CompanionHero.BattleEquipment,
            data.CompanionHero.CivilianEquipment
        );

        network.SendAll(message);
    }

    private void Handle_NetworkAdjustCompanionsEquipment(MessagePayload<NetworkAdjustCompanionsEquipment> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.CompanionHeroId, out var companionHero)) return;

            using (new AllowedThread())
            {
                companionHero._battleEquipment = data.BattleEquipment;
                companionHero._civilianEquipment = data.CivilianEquipment;
            }
        });
    }
}
