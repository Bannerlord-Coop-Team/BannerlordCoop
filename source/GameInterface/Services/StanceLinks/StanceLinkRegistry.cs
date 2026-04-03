using Common;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.StanceLinks;

/// <summary>
/// Registry for <see cref="StanceLink"/> type
/// </summary>
internal class StanceLinkRegistry : IAutoRegistry<StanceLink>
{
    ILogger Logger { get; }
    public StanceLinkRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(StanceLink));

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<StanceLink> registry)
    {
        // StanceLink instances are created dynamically during gameplay;
        // existing stances are re-registered via network sync on connection.
    }

    public void OnClientCreated(StanceLink obj, string id)
    {
    }

    public void OnClientDestroyed(StanceLink obj, string id)
    {
    }

    public void OnServerCreated(StanceLink obj, string id)
    {
    }

    public void OnServerDestroyed(StanceLink obj, string id)
    {
    }
}
