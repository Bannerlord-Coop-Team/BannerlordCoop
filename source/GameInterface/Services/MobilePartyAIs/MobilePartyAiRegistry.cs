using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
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

    public IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        foreach (var party in MobileParty.All)
        {
            objectManager.AddExisting(party.StringId, party.Ai);
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
