using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CharacterAttributes;
internal class CharacterAttributeRegistry : IAutoRegistry<CharacterAttribute>
{
    ILogger Logger { get; }
    public CharacterAttributeRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(CharacterAttribute), new Type[] { typeof(string) })
    };

    // TODO find destructor for banner effects
    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<CharacterAttribute> registry)
    {
        foreach (var obj in MBObjectManager.Instance.GetObjectTypeList<CharacterAttribute>())
        {
            registry.RegisterNewObject(obj, out _);
        }
    }

    public void OnClientCreated(CharacterAttribute obj, string id)
    {
    }

    public void OnClientDestroyed(CharacterAttribute obj, string id)
    {
    }

    public void OnServerCreated(CharacterAttribute obj, string id)
    {
    }

    public void OnServerDestroyed(CharacterAttribute obj, string id)
    {
    }
}

