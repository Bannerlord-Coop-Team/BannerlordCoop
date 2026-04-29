using Common;
using Common.Util;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MobilePartyAIs;
internal class MobilePartyAiRegistry : IAutoRegistry<MobilePartyAi>
{
    ILogger Logger { get; }
    public MobilePartyAiRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        foreach (var party in MobileParty.All)
        {
            objectManager.AddExisting($"{typeof(MobilePartyAi).Name}_{party.StringId}", party.Ai);
        }
    }

    public void OnClientCreated(MobilePartyAi obj, string id)
    {
    }

    public void OnClientDestroyed(MobilePartyAi obj, string id)
    {
    }

    public void OnServerCreated(MobilePartyAi obj, string id)
    {
    }

    public void OnServerDestroyed(MobilePartyAi obj, string id)
    {
    }
}
