using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Ships.ShipUpgradePieces;

internal class ShipUpgradePieceRegistry : AutoRegistryBase<ShipUpgradePiece>
{
    public ShipUpgradePieceRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(ShipUpgradePiece));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var shipUpgradePiece in MBObjectManager.Instance.GetObjects<ShipUpgradePiece>(x => true))
        {
            RegisterExistingObject(shipUpgradePiece.StringId, shipUpgradePiece);
        }
    }

    public override void OnClientCreated(ShipUpgradePiece obj, string id)
    {
    }

    public override void OnClientDestroyed(ShipUpgradePiece obj, string id)
    {
    }

    public override void OnServerCreated(ShipUpgradePiece obj, string id)
    {
    }

    public override void OnServerDestroyed(ShipUpgradePiece obj, string id)
    {
    }
}
