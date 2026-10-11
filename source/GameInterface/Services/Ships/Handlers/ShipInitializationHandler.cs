using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Ships.Messages;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;

namespace GameInterface.Services.Ships.Handlers;

/// <summary>
/// Replicates the state the <see cref="Ship"/> constructor sets, which a client's constructor-skipped copy lacks.
/// </summary>
internal class ShipInitializationHandler : IHandler
{
    private static readonly FieldInfo ShipHullField = AccessTools.Field(typeof(Ship), nameof(Ship.ShipHull));

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ShipInitializationHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<ShipInitialized>(Handle_ShipInitialized);
        messageBroker.Subscribe<NetworkInitializeShip>(Handle_NetworkInitializeShip);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ShipInitialized>(Handle_ShipInitialized);
        messageBroker.Unsubscribe<NetworkInitializeShip>(Handle_NetworkInitializeShip);
    }

    // Sent synchronously so it stays between the registry create and the owner change on the wire.
    private void Handle_ShipInitialized(MessagePayload<ShipInitialized> payload)
    {
        var ship = payload.What.Ship;
        if (!objectManager.TryGetHandleWithLogging(ship, out var shipHandle)) return;
        if (!objectManager.TryGetHandleWithLogging(ship.ShipHull, out var hullHandle)) return;

        network.SendAll(new NetworkInitializeShip(shipHandle, hullHandle, ship.RandomValue, ship._hitPoints, ship._sailHitPoints));
    }

    private void Handle_NetworkInitializeShip(MessagePayload<NetworkInitializeShip> payload)
    {
        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Ship>(message.ShipHandle, out var ship)) return;
            if (!objectManager.TryGetObjectWithLogging<ShipHull>(message.HullHandle, out var hull)) return;

            using (new AllowedThread())
            {
                // Readonly field, hence reflection.
                ShipHullField.SetValue(ship, hull);
                if (ship._shipPieces.Count == 0)
                    ship.InitializeFromTemplate(hull);
                ship.RandomValue = message.RandomValue;
                ship._hitPoints = message.HitPoints;
                ship._sailHitPoints = message.SailHitPoints;
                ship.UpdateVersionNo();
            }
        }, context: nameof(NetworkInitializeShip));
    }
}
