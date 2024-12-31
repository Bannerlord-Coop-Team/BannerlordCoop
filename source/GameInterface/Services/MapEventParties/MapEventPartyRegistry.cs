using GameInterface.Services.Registry;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties;
internal class MapEventPartyRegistry : RegistryBase<MapEventParty>
{
    private const string MapEventPartyIdPrefix = "CoopPartyBase";
    private static int InstanceCounter = 0;

    public MapEventPartyRegistry(IRegistryCollection collection) : base(collection)
    {
    }

    public override void RegisterAll()
    {
        foreach (MapEvent mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            foreach (MapEventParty mep in mapEvent.PartiesOnSide(TaleWorlds.Core.BattleSideEnum.None))
            {
                if (RegisterNewObject(mep, out var newId) == false)
                {
                    Logger.Error("Unable to register MapEventParty {id} in the object manager", mep.ToString());
                }
            }
            foreach (MapEventParty mep in mapEvent.PartiesOnSide(TaleWorlds.Core.BattleSideEnum.Attacker))
            {
                if (RegisterNewObject(mep, out var newId) == false)
                {
                    Logger.Error("Unable to register MapEventParty {id} in the object manager", mep.ToString());
                }
            }
            foreach (MapEventParty mep in mapEvent.PartiesOnSide(TaleWorlds.Core.BattleSideEnum.Defender))
            {
                if (RegisterNewObject(mep, out var newId) == false)
                {
                    Logger.Error("Unable to register MapEventParty {id} in the object manager", mep.ToString());
                }
            }
        }
    }

    protected override string GetNewId(MapEventParty obj)
    {
        return $"{MapEventPartyIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}
