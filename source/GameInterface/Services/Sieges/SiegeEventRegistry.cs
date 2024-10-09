using GameInterface.Services.Registry;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.Sieges;

/// <summary>
/// Registry for <see cref="SiegeEvent"/> objects
/// </summary>
internal class SiegeEventRegistry : RegistryBase<SiegeEvent>
{
    private const string SeigeEventPrefix = $"Coop{nameof(SiegeEvent)}";
    private static int InstanceCounter = 0;

    public SiegeEventRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        foreach(var siegeEvent in Campaign.Current.SiegeEventManager.SiegeEvents)
        {
            RegisterNewObject(siegeEvent, out _);
        }
    }

    protected override string GetNewId(SiegeEvent party)
    {
        return $"{SeigeEventPrefix}_{ Interlocked.Increment(ref InstanceCounter)}";
    }
}