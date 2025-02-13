using GameInterface.Services.Registry;
using System.Threading;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.SiegeStrategies;
internal class SiegeStrategyRegistry : RegistryBase<SiegeStrategy>
{
    private const string IdPrefix = "CoopSiegeStrategy";
    private int InstanceCounter = 0;

    public SiegeStrategyRegistry(IRegistryCollection collection) : base(collection)
    {
    }

    public override void RegisterAll()
    {
        foreach(var siegeStrategy in MBObjectManager.Instance.GetObjectTypeList<SiegeStrategy>())
        {
            if (base.RegisterNewObject(siegeStrategy, out var _) == false) Logger.Error($"Unable to register {nameof(SiegeStrategy)}");
        }
    }

    protected override string GetNewId(SiegeStrategy obj)
    {
        return $"{IdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}
