using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CharacterAttributes;
internal class CharacterAttributeRegistry : AutoRegistryBase<CharacterAttribute>
{
    public CharacterAttributeRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(CharacterAttribute), new Type[] { typeof(string) })
    };

    // TODO find destructor for banner effects
    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var obj in MBObjectManager.Instance.GetObjectTypeList<CharacterAttribute>())
        {
            RegisterExistingObject(obj.StringId, obj);
        }
    }

    public override void OnClientCreated(CharacterAttribute obj, string id)
    {
    }

    public override void OnClientDestroyed(CharacterAttribute obj, string id)
    {
    }

    public override void OnServerCreated(CharacterAttribute obj, string id)
    {
    }

    public override void OnServerDestroyed(CharacterAttribute obj, string id)
    {
    }
}

