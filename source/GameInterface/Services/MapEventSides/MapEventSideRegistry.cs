using GameInterface.Registry;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventSides;

/// <summary>
/// Registry for <see cref="MapEventSide"/> objects
/// </summary>
internal class MapEventSideRegistry : RegistryBase<MapEventSide>
{
    private const string MapEventSideIdPrefix = "CoopMapEventSide";
    private int InstanceCounter = 0;

    public MapEventSideRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        foreach (MapEvent mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            int counter = 1;

            foreach (var side in mapEvent._sides)
            {
                if (side == null) continue;

                var networkId = nameof(MapEventSide) + "_" + mapEvent.StringId + "_" + counter++;

                if (RegisterExistingObject(networkId, side) == false)
                    Logger.Error("Unable to register MapEventSide {id} in the object manager", side.ToString());
            }
        }
    }

    protected override string GetNewId(MapEventSide mapEventSide)
    {
        return $"{MapEventSideIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}

