using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties;
internal class MapEventPartyRegistry : AutoRegistryBase<MapEventParty>
{
    public MapEventPartyRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (MapEvent mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            foreach (var side in mapEvent._sides.Where(x => x != null))
            {
                foreach (var party in side.Parties.Where(x => x != null))
                {
                    RegisterExistingObject(mapEvent.StringId, party);
                }
            }
        }
    }

    public override void OnClientCreated(MapEventParty obj, string id)
    {
    }

    public override void OnClientDestroyed(MapEventParty obj, string id)
    {
    }

    public override void OnServerCreated(MapEventParty obj, string id)
    {
    }

    public override void OnServerDestroyed(MapEventParty obj, string id)
    {
    }
}
