using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.MBBodyProperties;
internal class MBBodyPropertyRegistry : IAutoRegistry<MBBodyProperty>
{
    ILogger Logger { get; }
    public MBBodyPropertyRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(MBBodyProperty))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<MBBodyProperty> registry)
    {
        foreach (CharacterObject character in Campaign.Current.Characters)
        {
            registry.RegisterNewObject(character.BodyPropertyRange, out _);
        }
    }

    public void OnClientCreated(MBBodyProperty obj, string id)
    {
    }

    public void OnClientDestroyed(MBBodyProperty obj, string id)
    {
    }

    public void OnServerCreated(MBBodyProperty obj, string id)
    {
    }

    public void OnServerDestroyed(MBBodyProperty obj, string id)
    {
    }
}
