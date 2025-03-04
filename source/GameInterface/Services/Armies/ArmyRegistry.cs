using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.Armies;

/// <summary>
/// Registry for <see cref="Army"/> type
/// </summary>
internal class ArmyRegistry : IAutoRegistry<Army>
{
    ILogger Logger { get; }
    public ArmyRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Army), new Type[] { typeof(Kingdom), typeof(MobileParty), typeof(Army.ArmyTypes) })
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<Army> registry)
    {
        IEnumerable<Kingdom> kingdoms = Campaign.Current?.Kingdoms ?? Enumerable.Empty<Kingdom>();

        List<Army> armies = new List<Army>();

        foreach (var kingdom in kingdoms.OrderBy(obj => obj.Id))
        {
            armies.AddRange(kingdom.Armies);
        }

        foreach (var army in armies)
        {
            registry.RegisterNewObject(army, out var _);
        }
    }

    public void OnClientCreated(Army obj, string id)
    {
    }

    public void OnClientDestroyed(Army obj, string id)
    {
    }

    public void OnServerCreated(Army obj, string id)
    {
    }

    public void OnServerDestroyed(Army obj, string id)
    {
    }
}
