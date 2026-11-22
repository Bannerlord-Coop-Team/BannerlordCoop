using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Monsters;

internal class MonsterRegistry : AutoRegistryBase<Monster>
{
    // MonsterLifetimePatches owns constructor synchronization.
    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();
    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public MonsterRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override void RegisterAllObjects()
    {
        foreach (Monster monster in MBObjectManager.Instance.GetObjectTypeList<Monster>())
        {
            RegisterExistingObject(monster.StringId, monster);
        }
    }

    public override void OnClientCreated(Monster obj, string id) { }
    public override void OnClientDestroyed(Monster obj, string id) { }
    public override void OnServerCreated(Monster obj, string id) { }
    public override void OnServerDestroyed(Monster obj, string id) { }
}
