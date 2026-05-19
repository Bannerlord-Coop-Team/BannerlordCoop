using Common;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.MBBodyProperties;
internal class MBBodyPropertyRegistry : AutoRegistryBase<MBBodyProperty>
{
    public MBBodyPropertyRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(MBBodyProperty))
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (CharacterObject character in CharacterObject.All.DistinctBy(c => c.BodyPropertyRange))
        {
            RegisterExistingObject(character.StringId, character.BodyPropertyRange);
        }
    }

    public override void OnClientCreated(MBBodyProperty obj, string id)
    {
    }

    public override void OnClientDestroyed(MBBodyProperty obj, string id)
    {
    }

    public override void OnServerCreated(MBBodyProperty obj, string id)
    {
    }

    public override void OnServerDestroyed(MBBodyProperty obj, string id)
    {
    }
}
