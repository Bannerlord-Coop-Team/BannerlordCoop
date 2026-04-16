using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEngineMissiles.Messages;
using Serilog;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEngineMissiles.Handlers;

internal class SiegeEngineMissileLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEngineMissileLifetimeHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    public SiegeEngineMissileLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<SiegeEngineMissileCreated>(HandleCreatedEvent);
        messageBroker.Subscribe<NetworkCreateSiegeEngineMissile>(HandleCreateCommand);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SiegeEngineMissileCreated>(HandleCreatedEvent);
        messageBroker.Unsubscribe<NetworkCreateSiegeEngineMissile>(HandleCreateCommand);
    }

    private void HandleCreatedEvent(MessagePayload<SiegeEngineMissileCreated> payload)
    {
        if (!objectManager.AddNewObject(payload.What.Data, out var id))
        {
            Logger.Error("Failed to AddNewObject on {EventHandler}", nameof(SiegeEngineMissileCreated));
            return;
        }

        network.SendAll(new NetworkCreateSiegeEngineMissile(id));
    }

    private void HandleCreateCommand(MessagePayload<NetworkCreateSiegeEngineMissile> payload)
    {
        var newSiegeEngineMissile = ObjectHelper.SkipConstructor<SiegeEvent.SiegeEngineMissile>();

        if (!objectManager.AddExisting(payload.What.SiegeEngineMissileId, newSiegeEngineMissile))
        {
            Logger.Error("Failed to create {ObjectName} on {EventHandler}", nameof(SiegeEvent.SiegeEngineMissile), nameof(NetworkCreateSiegeEngineMissile));
            return;
        }
    }
}