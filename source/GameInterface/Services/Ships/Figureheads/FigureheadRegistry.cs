using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Ships.Figureheads;

internal class FigureheadRegistry : AutoRegistryBase<Figurehead>
{
    public FigureheadRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(Figurehead));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var figureHead in MBObjectManager.Instance.GetObjects<Figurehead>(x => true))
        {
            RegisterExistingObject(figureHead.StringId, figureHead);
        }
    }

    public override void OnClientCreated(Figurehead obj, string id)
    {
    }

    public override void OnClientDestroyed(Figurehead obj, string id)
    {
    }

    public override void OnServerCreated(Figurehead obj, string id)
    {
    }

    public override void OnServerDestroyed(Figurehead obj, string id)
    {
    }
}
