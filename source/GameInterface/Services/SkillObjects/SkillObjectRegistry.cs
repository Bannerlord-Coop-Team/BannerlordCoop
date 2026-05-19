using Common;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.SkillObjects;
internal class SkillObjectRegistry : AutoRegistryBase<SkillObject>
{
    public SkillObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(SkillObject), new Type[] { typeof(string) })
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (SkillObject skill in MBObjectManager.Instance.GetObjectTypeList<SkillObject>())
        {
            RegisterExistingObject(skill.StringId, skill);
        }
    }

    public override void OnClientCreated(SkillObject obj, string id)
    {
    }

    public override void OnClientDestroyed(SkillObject obj, string id)
    {
    }

    public override void OnServerCreated(SkillObject obj, string id)
    {
    }

    public override void OnServerDestroyed(SkillObject obj, string id)
    {
    }
}