using Coop.Naval.Storms.Interfaces;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using NavalDLC.Map;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Coop.Naval.Storms;

internal class StormRegistry : AutoRegistryBase<Storm>
{
    private readonly IStormInterface stormInterface;

    public StormRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager, IStormInterface stormInterface)
        : base(logger, autoRegistryFactory, objectManager)
    {
        this.stormInterface = stormInterface;
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(Storm));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        // Clients load storms in the same order when joining
        var storms = stormInterface.SpawnedStorms;
        for (int i = 0; i < storms.Count; i++)
        {
            RegisterExistingObject(i.ToString(), storms[i]);
        }
    }

    public override void OnClientCreated(Storm obj, string id)
    {
        // Skipped constructor leaves the trail null until the first state message arrives
        obj._previousPositionsAndRadius = new Storm.PreviousData[Storm.PreviousPositionsCount];
    }

    public override void OnClientDestroyed(Storm obj, string id)
    {
        stormInterface.RemoveSpawnedStorm(obj);
    }

    public override void OnServerCreated(Storm obj, string id)
    {
    }

    public override void OnServerDestroyed(Storm obj, string id)
    {
    }
}
