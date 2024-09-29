using GameInterface.Services.Registry;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Registry for <see cref="MapEventSide"/> objects
/// </summary>
internal class MapEventSideRegistry : RegistryBase<MapEventSide>
{
    private const string MapEventSideIdPrefix = "CoopMapEventSide";
    private static int InstanceCounter = 0;

    public MapEventSideRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        foreach (var side in Campaign.Current.MapEventManager.MapEvents.SelectMany(mapEvent => mapEvent._sides))
        {
            if (side == null) continue;

            if (RegisterNewObject(side, out var _) == false)
            {
                Logger.Error($"Unable to register {side}");
            }
        }
    }

    protected override string GetNewId(MapEventSide mapEventSide)
    {
        return $"{MapEventSideIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}

