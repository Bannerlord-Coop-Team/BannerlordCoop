using GameInterface.Registry;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties;
internal class MapEventPartyRegistry : RegistryBase<MapEventParty>
{
    private const string MapEventPartyIdPrefix = "CoopPartyBase";
    private static int InstanceCounter = 0;

    public MapEventPartyRegistry(IRegistryCollection collection) : base(collection)
    {
    }

    public override void RegisterAll()
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

                    if (RegisterExistingObject(networkId, party) == false)
                        Logger.Error("Unable to register MapEventParty {id} in the object manager", party.ToString());
                }
            }
        }
    }

    protected override string GetNewId(MapEventParty obj)
    {
        return $"{MapEventPartyIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}
