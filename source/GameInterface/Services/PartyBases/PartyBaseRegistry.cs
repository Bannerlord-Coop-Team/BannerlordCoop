using GameInterface.Services.Registry;
using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyBases;
internal class PartyBaseRegistry : RegistryBase<PartyBase>
{
    public PartyBaseRegistry(IRegistryCollection collection) : base(collection)
    {
    }

    public override void RegisterAll()
    {
        foreach (var party in MobileParty.All)
        {
            if(RegisterNewObject(party.Party, out var _) == false)
            {
                Logger.Error("Unable to register PartyBase from Party with id {id} in the object manager", party.StringId);
            }
        }
    }

    protected override string GetNewId(PartyBase obj)
    {
        return Guid.NewGuid().ToString();
    }
}
