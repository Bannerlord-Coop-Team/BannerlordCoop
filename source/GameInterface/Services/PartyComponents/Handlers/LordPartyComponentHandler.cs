using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.Handlers;

internal class LordPartyComponentHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    private static readonly ILogger Logger = LogManager.GetLogger<LordPartyComponentHandler>();

    public LordPartyComponentHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<LordPartyComponentInitArgsUpdated>(Handle_LordPartyComponentInitArgsUpdated);
        messageBroker.Subscribe<NetworkUpdateLordPartyComponentInitArgs>(Handle_NetworkUpdateLordPartyComponentInitArgs);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LordPartyComponentInitArgsUpdated>(Handle_LordPartyComponentInitArgsUpdated);
        messageBroker.Unsubscribe<NetworkUpdateLordPartyComponentInitArgs>(Handle_NetworkUpdateLordPartyComponentInitArgs);
    }

    private void Handle_LordPartyComponentInitArgsUpdated(MessagePayload<LordPartyComponentInitArgsUpdated> payload)
    {
        var instance = payload.What.Instance;
        var initArgs = payload.What.InitArgs;

        if (!objectManager.TryGetIdWithLogging(instance, out var lordPartyComponentId)) return;

        // SpawnSettlement is optional; only resolve an id when one is present.
        string spawnSettlementId = null;
        if (initArgs.SpawnSettlement != null &&
            !objectManager.TryGetIdWithLogging(initArgs.SpawnSettlement, out spawnSettlementId)) return;

        network.SendAll(new NetworkUpdateLordPartyComponentInitArgs(
            lordPartyComponentId,
            initArgs.Position,
            initArgs.SpawnRadius,
            spawnSettlementId
        ));
    }

    private void Handle_NetworkUpdateLordPartyComponentInitArgs(MessagePayload<NetworkUpdateLordPartyComponentInitArgs> payload)
    {
        var message = payload.What;

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<LordPartyComponent>(message.LordPartyComponentId, out var instance)) return;

                Settlement spawnSettlement = null;
                if (message.SpawnSettlementId != null &&
                    !objectManager.TryGetObjectWithLogging(message.SpawnSettlementId, out spawnSettlement)) return;

                using (new AllowedThread())
                {
                    var initArgs = new LordPartyComponent.InitializationArgs(message.Position, message.SpawnRadius, spawnSettlement);

                    instance._initializationArgs = initArgs;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply NetworkUpdateLordPartyComponentInitArgs");
            }
        });
    }
}
