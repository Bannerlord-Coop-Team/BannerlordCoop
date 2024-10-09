using GameInterface.Services.Registry;
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
    private static int InstanceCounter = 0;

    public MapEventRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        foreach (var mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            if (RegisterNewObject(mapEvent, out var _) == false)
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

