using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Ships;

/// <summary>
/// Registry for the War Sails <see cref="ShipUpgradePiece"/> catalog, keyed by StringId so ship messages can address pieces.
/// </summary>
internal class ShipUpgradePieceRegistry : AutoRegistryBase<ShipUpgradePiece>
{
    public ShipUpgradePieceRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    // XML catalog loaded identically on every machine, never created mid-session.
    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var piece in MBObjectManager.Instance.GetObjectTypeList<ShipUpgradePiece>())
        {
            RegisterExistingObject(piece.StringId, piece);
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
