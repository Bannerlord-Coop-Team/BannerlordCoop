using Common;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.Alleys;
internal class AlleyRegistry : IAutoRegistry<Alley>
{
    ILogger Logger { get; }
    public AlleyRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Alley), new Type[] { typeof(Settlement), typeof(string), typeof(TextObject) })
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        
        foreach (Settlement settlement in Campaign.Current.Settlements)
        {
            if (settlement.Town == null) continue;

            int counter = 0;

            foreach (Alley alley in settlement.Alleys)
            {
                var networkId = settlement.StringId + counter++;
                if (!objectManager.AddExisting(networkId, alley))
                    Logger.Error($"Unable to register {alley}");
            }
        }
    }

    public void OnClientCreated(Alley obj, string id)
    {
    }

    public void OnClientDestroyed(Alley obj, string id)
    {
    }

    public void OnServerCreated(Alley obj, string id)
    {
    }

    public void OnServerDestroyed(Alley obj, string id)
    {
    }
}
