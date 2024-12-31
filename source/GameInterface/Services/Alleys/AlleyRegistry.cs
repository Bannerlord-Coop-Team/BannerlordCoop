using GameInterface.Services.Registry;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Alleys;

/// <summary>
/// Registry for <see cref="Alley"/> type
/// </summary>
internal class AlleyRegistry : RegistryBase<Alley>
{
    private const string AlleyIdPrefix = "CoopAlley";
    private static int InstanceCounter = 0;

    public AlleyRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        foreach(Settlement settlement in Campaign.Current.Settlements)
        {
            if(settlement.Town == null) continue;

            foreach(Alley alley in settlement.Alleys)
            {
                if (RegisterNewObject(alley, out var _) == false)
                {
                    Logger.Error($"Unable to register {alley}");
                }
            }
        }
    }

    protected override string GetNewId(Alley obj)
    {
        return $"{AlleyIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}
