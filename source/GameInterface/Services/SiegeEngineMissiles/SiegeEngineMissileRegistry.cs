using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEngineMissiles;
internal class SiegeEngineMissileRegistry : AutoRegistryBase<SiegeEvent.SiegeEngineMissile>
{
    public SiegeEngineMissileRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(SiegeEvent.SiegeEngineMissile));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        Logger.Warning("RegisterAllObjects is not implemented for SiegeEngineMissileRegistry");
    }

    public override void OnClientCreated(SiegeEvent.SiegeEngineMissile obj, string id)
    {
    }

    public override void OnClientDestroyed(SiegeEvent.SiegeEngineMissile obj, string id)
    {
    }

    public override void OnServerCreated(SiegeEvent.SiegeEngineMissile obj, string id)
    {
    }

    public override void OnServerDestroyed(SiegeEvent.SiegeEngineMissile obj, string id)
    {
    }
}
