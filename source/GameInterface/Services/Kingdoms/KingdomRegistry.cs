using Common;
using GameInterface.Services.Registry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.Kingdoms;

/// <summary>
/// Registry for <see cref="Kingdom"/> type
/// </summary>
/// 
internal class KingdomRegistry : RegistryBase<Kingdom>
{
    private const string KingdomIdPrefix = "CoopKingdom";
    private static int InstanceCounter = 0;

    public KingdomRegistry(IRegistryCollection collection) : base(collection)
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

        foreach (var kingdom in objectManager.Kingdoms)
        {
            RegisterExistingObject(kingdom.StringId, kingdom);
        }
    }

    protected override string GetNewId(Kingdom obj)
    {
        return $"{KingdomIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}

//internal class KingdomRegistry : RegistryBase<Kingdom>
//{
//    private const string PartyStringIdPrefix = "CoopKingdom";

//    public KingdomRegistry(IRegistryCollection collection) : base(collection) { }

//    public override void RegisterAll()
//    {
//        var objectManager = Campaign.Current?.CampaignObjectManager;

//        if (objectManager == null)
//        {
//            Logger.Error("Unable to register objects when CampaignObjectManager is null");
//            return;
//        }

//        foreach (var kingdom in objectManager.Kingdoms)
//        {
//            RegisterExistingObject(kingdom.StringId, kingdom);
//        }
//    }

//    protected override string GetNewId(Kingdom kingdom)
//    {
//        kingdom.StringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<Kingdom>(PartyStringIdPrefix);
//        return kingdom.StringId;
//    }
//}
