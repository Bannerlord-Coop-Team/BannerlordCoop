using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;

namespace GameInterface.Services.BattleWreckages;

internal class BattleWreckageRegistry : AutoRegistryBase<BattleWreckage>
{
    public BattleWreckageRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(BattleWreckage));

    public override IEnumerable<MethodBase> DestroyMethods => new MethodBase[]
    {
        AccessTools.Method(typeof(BattleWreckage), nameof(BattleWreckage.DestroyWreckage))
    };

    public override void RegisterAllObjects()
    {
        // Wreckages live only in CampaignObjectManager, MBObjectManager doesnt hold them
        foreach (var battleWreckage in Campaign.Current.CampaignObjectManager.Wreckages)
        {
            RegisterExistingObject(battleWreckage.StringId, battleWreckage);
        }
    }

    public override void OnClientCreated(BattleWreckage obj, string id)
    {
    }

    public override void OnClientDestroyed(BattleWreckage obj, string id)
    {
    }

    public override void OnServerCreated(BattleWreckage obj, string id)
    {
    }

    public override void OnServerDestroyed(BattleWreckage obj, string id)
    {
    }
}
