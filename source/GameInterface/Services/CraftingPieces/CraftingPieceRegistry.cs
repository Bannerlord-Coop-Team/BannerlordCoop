using Common;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CraftingPieces;

internal class CraftingPieceRegistry : AutoRegistryBase<CraftingPiece>
{
    public CraftingPieceRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(CraftingPiece));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var craftingPiece in MBObjectManager.Instance.GetObjects<CraftingPiece>(x => true))
        {
            objectManager.AddExisting(craftingPiece.StringId, craftingPiece);
        }
    }

    public override void OnClientCreated(CraftingPiece obj, string id)
    {
    }

    public override void OnClientDestroyed(CraftingPiece obj, string id)
    {
    }

    public override void OnServerCreated(CraftingPiece obj, string id)
    {
    }

    public override void OnServerDestroyed(CraftingPiece obj, string id)
    {
    }
}
