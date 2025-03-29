using Common;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs;
internal class MobilePartyAiRegistry : IAutoRegistry<MobilePartyAi>
{
    ILogger Logger { get; }
    public MobilePartyAiRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(MobilePartyAi), new Type[] { typeof(MobileParty) })
    };

    public IEnumerable<MethodBase> DestroyMethods => new MethodBase[]
    {
        //AccessTools.Method(typeof(MobileParty), nameof(MobileParty.RemoveParty)),
    };

    public void RegisterAllObjects(IRegistry<MobilePartyAi> registry)
    {
        var objectManager = Campaign.Current?.CampaignObjectManager;

        if (objectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        foreach (var party in objectManager.MobileParties)
        {
            var partyAi = party.Ai;

            if (partyAi == null)
            {
                Logger.Warning("{partyName}'s Ai was null when registering", party.Name);
                continue;
            }

            var networkId = $"{nameof(MobilePartyAi)}_{party.StringId}";
            registry.RegisterExistingObject(networkId, partyAi);
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
