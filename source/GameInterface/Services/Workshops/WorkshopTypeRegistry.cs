using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops;

internal class WorkshopTypeRegistry : AutoRegistryBase<WorkshopType>
{
    public WorkshopTypeRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(WorkshopType));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        // Old version broke save games? Maybe watch out for that
        foreach(WorkshopType workshopType in WorkshopType.All)
        {
            RegisterExistingObject(workshopType.StringId, workshopType);
        }
    }

    public override void OnClientCreated(WorkshopType obj, string id)
    {
    }

    public override void OnClientDestroyed(WorkshopType obj, string id)
    {
    }

    public override void OnServerCreated(WorkshopType obj, string id)
    {
    }

    public override void OnServerDestroyed(WorkshopType obj, string id)
    {
    }
}