using GameInterface.Services.Registry;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyBases;
internal class PartyBaseRegistry : RegistryBase<PartyBase>
{
    private const string PartyBaseIdPrefix = "CoopPartyBase";
    private static int InstanceCounter = 0;

    public PartyBaseRegistry(IRegistryCollection collection) : base(collection)
    {
    }

    public override void RegisterAll()
    {
        foreach (var party in MobileParty.All)
        {
            if(RegisterNewObject(party.Party, out var newId) == false)
            {
                Logger.Error("Unable to register PartyBase from Party with id {id} in the object manager", party.StringId);
            }
        }
    }

    protected override string GetNewId(PartyBase obj)
    {
        return $"{PartyBaseIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}
