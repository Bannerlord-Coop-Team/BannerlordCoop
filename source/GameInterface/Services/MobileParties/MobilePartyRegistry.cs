using Common;
using Coop.Mod.Extentions;
using GameInterface.Services.Registry;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties;

internal interface IMobilePartyRegistry : IRegistry<MobileParty>
{
    bool RegisterParty(MobileParty party);
    bool RemoveParty(MobileParty party);
    void RegisterAllParties();
}

internal class MobilePartyRegistry : RegistryBase<MobileParty>, IMobilePartyRegistry
{
    public bool RegisterParty(MobileParty party)
    {
        if (RegisterExistingObject(party.StringId, party) == false)
        {
            Logger.Warning("Unable to register party: {object}", party.Name);
            return false;
        }

        return true;
    }

    public bool RemoveParty(MobileParty party)
    {
        return Remove(party.StringId);
    }

    public void RegisterAllParties()
    {
        RegisterPartyListeners();

        var objectManager = Campaign.Current?.CampaignObjectManager;

        if (objectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        foreach (var party in objectManager.MobileParties)
        {
            RegisterParty(party);
        }
    }
    public void RegisterPartyListeners()
    {
        CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, Handle_MobilePartyCreated);
        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, Handle_MobilePartyDestroyed);
    }

    public void Handle_MobilePartyCreated(MobileParty party)
    {
        if (RegisterParty(party) && !party.IsAnyPlayerMainParty())
        {
            
        }
    }

    public void Handle_MobilePartyDestroyed(MobileParty party, PartyBase partyBase)
    {

    }
}
