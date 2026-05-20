using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventComponents;


internal class BlockadeBattleMapEventRegistry : AutoRegistryBase<BlockadeBattleMapEvent>
{
    public BlockadeBattleMapEventRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[]
    {
        AccessTools.Constructor(typeof(BlockadeBattleMapEvent), new Type[] { typeof(MapEvent) })
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void OnClientCreated(BlockadeBattleMapEvent obj, string id)
    {
    }

    public override void OnClientDestroyed(BlockadeBattleMapEvent obj, string id)
    {
    }

    public override void OnServerCreated(BlockadeBattleMapEvent obj, string id)
    {
    }

    public override void OnServerDestroyed(BlockadeBattleMapEvent obj, string id)
    {
    }

    public override void RegisterAllObjects()
    {
    }
}
