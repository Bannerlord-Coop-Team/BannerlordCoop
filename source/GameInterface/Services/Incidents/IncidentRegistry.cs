using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Incidents;

internal class IncidentRegistry : AutoRegistryBase<Incident>
{
    public IncidentRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(Incident));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var incident in MBObjectManager.Instance.GetObjects<Incident>(x => true))
        {
            RegisterExistingObject(incident.StringId, incident);
        }
    }

    public override void OnClientCreated(Incident obj, string id)
    {
    }

    public override void OnClientDestroyed(Incident obj, string id)
    {
    }

    public override void OnServerCreated(Incident obj, string id)
    {
    }

    public override void OnServerDestroyed(Incident obj, string id)
    {
    }
}
