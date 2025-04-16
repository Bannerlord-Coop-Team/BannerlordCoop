using GameInterface.Registry;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Registry for <see cref="MapEvent"/> objects
/// </summary>
internal class MapEventRegistry : RegistryBase<MapEvent>
{
    private const string MapEventIdPrefix = "CoopMapEvent";
    private int InstanceCounter = 0;

    public MapEventRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        foreach (var mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            if (mapEvent.StringId == null) return;

            if (RegisterExistingObject(mapEvent.StringId, mapEvent) == false)
            {
                Logger.Error($"Unable to register {mapEvent}");
            }
        }
    }

    protected override string GetNewId(MapEvent party)
    {
        return $"{MapEventIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}

