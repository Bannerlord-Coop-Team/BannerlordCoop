using Common;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Localization;

namespace GameInterface.Services.SiegeEngineMissiles;
internal class SiegeEngineMissileRegistry : IAutoRegistry<SiegeEvent.SiegeEngineMissile>
{
    ILogger Logger { get; }
    public SiegeEngineMissileRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(SiegeEvent.SiegeEngineMissile));

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
    {
    }

    public void OnClientCreated(SiegeEvent.SiegeEngineMissile obj, string id)
    {
    }

    public void OnClientDestroyed(SiegeEvent.SiegeEngineMissile obj, string id)
    {
    }

    public void OnServerCreated(SiegeEvent.SiegeEngineMissile obj, string id)
    {
    }

    public void OnServerDestroyed(SiegeEvent.SiegeEngineMissile obj, string id)
    {
    }
}
