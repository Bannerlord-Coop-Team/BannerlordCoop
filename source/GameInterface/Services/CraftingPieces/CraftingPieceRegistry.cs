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

internal class CraftingPieceRegistry : IAutoRegistry<CraftingPiece>
{
    ILogger Logger { get; }
    public CraftingPieceRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(CraftingPiece))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        foreach (var craftingPiece in MBObjectManager.Instance.GetObjects<CraftingPiece>(x => true))
        {
            objectManager.AddExisting(craftingPiece.StringId, craftingPiece);
        }
    }

    public void OnClientCreated(CraftingPiece obj, string id)
    {
    }

    public void OnClientDestroyed(CraftingPiece obj, string id)
    {
    }

    public void OnServerCreated(CraftingPiece obj, string id)
    {
    }

    public void OnServerDestroyed(CraftingPiece obj, string id)
    {
    }
}
