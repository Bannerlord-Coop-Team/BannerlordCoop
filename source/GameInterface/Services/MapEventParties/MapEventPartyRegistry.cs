using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties;
internal class MapEventPartyRegistry : IAutoRegistry<MapEventParty>
{
    ILogger Logger { get; }
    public MapEventPartyRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        foreach (MapEvent mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            int counter = 1;
            
            foreach (var side in mapEvent._sides)
            {
                if (side == null) continue;

                foreach (var party in side.Parties)
                {
                    if (party == null) continue;

                    var networkId = mapEvent.StringId + "_" + counter++;

                    if (objectManager.AddExisting(networkId, party) == false)
                        Logger.Error("Unable to register MapEventParty {id} in the object manager", party.ToString());
                }
            }
        }
    }

    public void OnClientCreated(MapEventParty obj, string id)
    {
    }

    public void OnClientDestroyed(MapEventParty obj, string id)
    {
    }

    public void OnServerCreated(MapEventParty obj, string id)
    {
    }

    public void OnServerDestroyed(MapEventParty obj, string id)
    {
    }
}
