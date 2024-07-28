using GameInterface.Services.Registry;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Hideouts;
internal class HideoutRegistry : RegistryBase<Hideout>
{
    public HideoutRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        var objectManager = Campaign.Current?.CampaignObjectManager;

        if (objectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        foreach (var settlement in Campaign.Current.CampaignObjectManager.Settlements.Where(settlement => settlement.IsHideout))
        {
            Hideout hideout = settlement.Hideout;
            base.RegisterExistingObject(hideout.StringId, hideout);
        }
    }

    protected override string GetNewId(Hideout hideout)
    {
        return Guid.NewGuid().ToString();
    }
}

