using Common;
using GameInterface.Services.Registry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Kingdoms;

internal class KingdomRegistry : RegistryBase<Kingdom>
{
    private const string PartyStringIdPrefix = "CoopKingdom";

    public KingdomRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        var objectManager = Campaign.Current?.CampaignObjectManager;

        if (objectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        foreach (var kingdom in objectManager.Kingdoms)
        {
            RegisterExistingObject(kingdom.StringId, kingdom);
        }
    }

    protected override string GetNewId(Kingdom kingdom)
    {
        kingdom.StringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<Kingdom>(PartyStringIdPrefix);
        return kingdom.StringId;
    }
}
