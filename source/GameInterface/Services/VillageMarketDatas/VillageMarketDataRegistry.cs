using GameInterface.Services.Registry;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.VillageMarketDatas;


/// <summary>
/// Registry manager for SiegeEvent
/// </summary>
internal class VillageMarketRegistry : RegistryBase<VillageMarketData>
{
    private const string MarketDataIdPrefix = "CoopMarketData";
    private static int InstanceCounter = 0;

    public VillageMarketRegistry(IRegistryCollection collection) : base(collection)
    {
    }

    public override void RegisterAll()
    {
        foreach (var village in Campaign.Current._villages)
        {
            RegisterNewObject(village._marketData, out _);
        }
    }

    protected override string GetNewId(VillageMarketData obj)
    {
        return $"{MarketDataIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}