using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.PerkObjects;
internal class ItemCategoryRegistry : AutoRegistryBase<PerkObject>
{
    public ItemCategoryRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(PerkObject), new Type[] { typeof(string) })
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (PerkObject trait in PerkObject.All)
        {
            RegisterExistingObject(trait.StringId, trait);
        }
    }

    public override void OnClientCreated(PerkObject obj, string id)
    {
    }

    public override void OnClientDestroyed(PerkObject obj, string id)
    {
    }

    public override void OnServerCreated(PerkObject obj, string id)
    {
    }

    public override void OnServerDestroyed(PerkObject obj, string id)
    {
    }
}