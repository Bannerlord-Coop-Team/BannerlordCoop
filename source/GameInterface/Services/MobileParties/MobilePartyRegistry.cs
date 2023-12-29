using Common;
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

    public bool RemoveParty(MobileParty party) => Remove(party.StringId);

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
            RegisterParty(party);
        }
    }

    private const string PartyStringIdPrefix = "CoopParty";
    public override bool RegisterNewObject(MobileParty obj, out string id)
    {
        id = null;

        if (Campaign.Current?.CampaignObjectManager == null) return false;

        var newId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<MobileParty>(PartyStringIdPrefix);

        if (objIds.ContainsKey(newId)) return false;

        obj.StringId = newId;

        objIds.Add(newId, obj);

        id = newId;

        return true;
    }
}
