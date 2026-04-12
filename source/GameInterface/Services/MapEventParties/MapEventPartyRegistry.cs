using Common;
using Common.Logging;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEventParties;

/// <summary>
/// Registry for <see cref="MapEventParty"/> objects
/// </summary>
internal class MapEventPartyRegistry : IAutoRegistry<MapEventParty>
{
    public MapEventPartyRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    private static ILogger Logger = LogManager.GetLogger<MapEventParty>();

    public IEnumerable<MethodBase> Constructors => new MethodBase[] { AccessTools.Constructor(typeof(MapEventParty), new Type[] { typeof(PartyBase) }) };

    public IEnumerable<MethodBase> DestroyMethods => new MethodBase[] { };

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

    public void RegisterAllObjects(IRegistry<MapEventParty> registry)
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

                    var networkId = nameof(MapEventParty) + "_" + mapEvent.StringId + "_" + counter++;

                    if (registry.RegisterExistingObject(networkId, party) == false)
                        Logger.Error("Unable to register MapEventParty {id} in the object manager", party.ToString());
                }
            }
        }
    }
}