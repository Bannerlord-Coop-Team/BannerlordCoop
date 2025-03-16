using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.BesiegerCamps;
internal class BesiegerCampRegistry : IAutoRegistry<BesiegerCamp>
{
    ILogger Logger { get; }
    public BesiegerCampRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(BesiegerCamp));

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<BesiegerCamp> registry)
    {
        foreach (var camp in Campaign.Current.SiegeEventManager.SiegeEvents.Select(siegeEvent => siegeEvent.BesiegerCamp))
        {
            if (registry.RegisterNewObject(camp, out _) == false) Logger.Error($"Unable to register {camp}");
        }
    }

    public void OnClientCreated(BesiegerCamp obj, string id)
    {
        AccessTools.Field(typeof(BesiegerCamp), nameof(BesiegerCamp._besiegerParties)).SetValue(obj, new MBList<MobileParty>());
    }

    public void OnClientDestroyed(BesiegerCamp obj, string id)
    {
    }

    public void OnServerCreated(BesiegerCamp obj, string id)
    {
    }

    public void OnServerDestroyed(BesiegerCamp obj, string id)
    {
    }
}
