using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

namespace GameInterface.Services.SiegeEvents;
internal class SiegeEventRegistry : IAutoRegistry<SiegeEvent>
{
    ILogger Logger { get; }
    public SiegeEventRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(SiegeEvent), new Type[] { typeof(Settlement), typeof(MobileParty) })
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<SiegeEvent> registry)
    {
        foreach (var instance in Campaign.Current.SiegeEventManager.SiegeEvents)
        {
            if (registry.RegisterNewObject(instance, out var _) == false) Logger.Error($"Unable to register {nameof(SiegeEvent)}");
        }
    }

    public void OnClientCreated(SiegeEvent obj, string id)
    {

    }

    public void OnClientDestroyed(SiegeEvent obj, string id)
    {
    }

    public void OnServerCreated(SiegeEvent obj, string id)
    {
    }

    public void OnServerDestroyed(SiegeEvent obj, string id)
    {
    }
}
