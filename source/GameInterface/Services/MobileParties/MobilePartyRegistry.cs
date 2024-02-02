using Common;
using GameInterface.Services.Registry;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties;

internal interface IMobilePartyRegistry : IRegistry<MobileParty>
{
    void RegisterAllParties();
}

internal class MobilePartyRegistry : RegistryBase<MobileParty>, IMobilePartyRegistry
{

    public void RegisterAllParties()
    {
        var objectManager = Campaign.Current?.CampaignObjectManager;

        if (objectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        foreach (var party in objectManager.MobileParties)
        {
            RegisterExistingObject(party.StringId, party);
        }
    }

    private const string PartyStringIdPrefix = "CoopParty";
    protected override string GetNewId(MobileParty party)
    {
        party.StringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<MobileParty>(PartyStringIdPrefix);
        return party.StringId;
    }
}
