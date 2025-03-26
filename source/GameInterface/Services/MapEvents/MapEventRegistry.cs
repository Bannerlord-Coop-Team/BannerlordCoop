using GameInterface.Registry;
using System.Diagnostics.Metrics;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Settlements;

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
            int counter = 0;
            var networkId = nameof(Building) + "_" + "Coop" + counter++;
            if (RegisterExistingObject(networkId, mapEvent) == false)
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

