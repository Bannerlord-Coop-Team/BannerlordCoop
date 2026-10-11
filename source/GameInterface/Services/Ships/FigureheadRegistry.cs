using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Ships;

/// <summary>
/// Registry for the War Sails <see cref="Figurehead"/> catalog, keyed by StringId so ship and unlock messages can address figureheads.
/// </summary>
internal class FigureheadRegistry : AutoRegistryBase<Figurehead>
{
    public FigureheadRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    // XML catalog loaded identically on every machine, never created mid-session.
    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var figurehead in MBObjectManager.Instance.GetObjectTypeList<Figurehead>())
        {
            RegisterExistingObject(figurehead.StringId, figurehead);
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
