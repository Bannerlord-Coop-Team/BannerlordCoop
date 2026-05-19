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

namespace GameInterface.Services.FieldBattleEventComponents;


internal class FieldBattleEventComponentRegistry : AutoRegistryBase<FieldBattleEventComponent>
{
    public FieldBattleEventComponentRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[]
    {
        AccessTools.Constructor(typeof(FieldBattleEventComponent), new Type[] { typeof(MapEvent) })
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void OnClientCreated(FieldBattleEventComponent obj, string id)
    {
    }

    public override void OnClientDestroyed(FieldBattleEventComponent obj, string id)
    {
    }

    public override void OnServerCreated(FieldBattleEventComponent obj, string id)
    {
    }

    public override void OnServerDestroyed(FieldBattleEventComponent obj, string id)
    {
    }

    public override void RegisterAllObjects()
    {
    }
}
