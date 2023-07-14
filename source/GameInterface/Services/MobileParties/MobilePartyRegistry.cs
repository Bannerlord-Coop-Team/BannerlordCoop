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
        var objectManager = Campaign.Current?.CampaignObjectManager;

        if (objectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        var parties = objectManager.MobileParties.ToArray();

        foreach (var party in parties)
        {
            RegisterParty(party);
        }
    }
}
