using GameInterface.Services.Registry;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs;
internal class MobilePartyAiRegistry : RegistryBase<MobilePartyAi>
{
    public MobilePartyAiRegistry(IRegistryCollection collection) : base(collection)
    {
    }

    public override void RegisterAll()
    {
        var objectManager = Campaign.Current?.CampaignObjectManager;

        if (objectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        foreach (var party in objectManager.MobileParties)
        {
            var partyAi = party.Ai;

            if (partyAi == null)
            {
                Logger.Warning("{partyName}'s Ai was null when registering", party.Name);
            }

            var newId = GetNewId(partyAi);

            base.RegisterExistingObject(newId, partyAi);
        }
    }

    protected override string GetNewId(MobilePartyAi obj)
    {
        return Guid.NewGuid().ToString();
    }
}
