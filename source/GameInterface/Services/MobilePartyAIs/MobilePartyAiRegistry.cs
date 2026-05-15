using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs;
internal class MobilePartyAiRegistry : AutoRegistryBase<MobilePartyAi>
{
    public MobilePartyAiRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var party in MobileParty.All)
        {
            if (party?.Ai == null) continue;
            RegisterExistingObject(party.StringId, party.Ai);
        }
    }

    public override void OnClientCreated(MobilePartyAi obj, string id)
    {
    }

    public override void OnClientDestroyed(MobilePartyAi obj, string id)
    {
    }

    public override void OnServerCreated(MobilePartyAi obj, string id)
    {
    }

    public override void OnServerDestroyed(MobilePartyAi obj, string id)
    {
    }
}
