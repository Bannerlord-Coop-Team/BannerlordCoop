using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

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

    public IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(Army));

    public IEnumerable<MethodBase> DestroyMethods => new MethodBase[]
    {
        AccessTools.Method(typeof(Army), nameof(Army.DisperseInternal))
    };

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        IEnumerable<Kingdom> kingdoms = Campaign.Current?.Kingdoms ?? Enumerable.Empty<Kingdom>();

        
        foreach (var kingdom in kingdoms)
        {
            int counter = 1;
            foreach (var army in kingdom.Armies)
            {
                var networkId = $"{kingdom.StringId}_{counter++}";
                objectManager.AddExisting(networkId, army);
            }
        }
    }

    public void OnClientCreated(Army obj, string id)
    {
        AccessTools.Field(typeof(Army), nameof(Army._parties)).SetValue(obj, new MBList<MobileParty>());
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
