using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Messages;
using Serilog;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.Handlers;

internal class VillagerPartyComponentHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    private static readonly ILogger Logger = LogManager.GetLogger<VillagerPartyComponentHandler>();

    public VillagerPartyComponentHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<VillagerPartyVillageChanged>(Handle_VillagerPartyVillageChanged);
        messageBroker.Subscribe<NetworkVillagerPartyVillageChanged>(Handle_NetworkVillagerPartyVillageChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VillagerPartyVillageChanged>(Handle_VillagerPartyVillageChanged);
        messageBroker.Unsubscribe<NetworkVillagerPartyVillageChanged>(Handle_NetworkVillagerPartyVillageChanged);
    }

    private void Handle_VillagerPartyVillageChanged(MessagePayload<VillagerPartyVillageChanged> payload)
    {
        var instance = payload.What.Instance;
        var village = payload.What.Village;

        if (!objectManager.TryGetIdWithLogging(instance, out var villagerPartyComponentId)) return;
        if (!objectManager.TryGetIdWithLogging(village, out var villageId)) return;

        network.SendAll(new NetworkVillagerPartyVillageChanged(villagerPartyComponentId, villageId));
    }

    private void Handle_NetworkVillagerPartyVillageChanged(MessagePayload<NetworkVillagerPartyVillageChanged> payload)
    {
        var message = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<VillagerPartyComponent>(message.VillagerPartyComponentId, out var instance)) return;

            if (!objectManager.TryGetObjectWithLogging<Village>(message.VillageId, out var village)) return;

            using (new AllowedThread())
            {
                instance.Village = village;
            }
        });
    }
}
