using GameInterface.Services.Registry;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps;

internal class BeseigerCampRegistry : RegistryBase<BesiegerCamp>
{
    private const string BeseigerCampIdPrefix = "CoopBeseigerCamp";
    private static int InstanceCounter = 0;

    public BeseigerCampRegistry(IRegistryCollection collection) : base(collection)
    {
    }

    public override void RegisterAll()
    {
        foreach (var camp in Campaign.Current.SiegeEventManager.SiegeEvents.Select(siegeEvent => siegeEvent.BesiegerCamp))
        {
            if (RegisterNewObject(camp, out _) == false)
            {
                Logger.Error($"Unable to register {camp}");
            }
        }
    }

    protected override string GetNewId(BesiegerCamp obj)
    {
        return $"{BeseigerCampIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}