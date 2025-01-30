using System.Threading;
using GameInterface.Services.Registry;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.VillageTypes;

/// <summary>
/// Registry for <see cref="VillageType"/> type
/// </summary>
internal class VillageTypeRegistry : RegistryBase<VillageType>
{
    private const string VillageTypeIdPrefix = "CoopVillageType";
    private static int InstanceCounter = 0;

    public VillageTypeRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        foreach (Settlement settlement in Campaign.Current.Settlements)
        {
            if (RegisterNewObject(settlement.Village.VillageType, out var _) == false)
            {
                Logger.Error($"Unable to register {settlement.Village.VillageType}");
            }
        }
    }

    protected override string GetNewId(VillageType obj)
    {
        return $"{VillageTypeIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}
