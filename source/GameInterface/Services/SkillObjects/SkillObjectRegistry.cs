using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.SkillObjects;
internal class SkillObjectRegistry : IAutoRegistry<SkillObject>
{
    ILogger Logger { get; }
    public SkillObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(SkillObject), new Type[] { typeof(string) })
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<SkillObject> registry)
    {
        foreach (SkillObject skill in MBObjectManager.Instance.GetObjectTypeList<SkillObject>())
        {
            registry.RegisterExistingObject(skill.StringId, skill);
        }
    }

    public void OnClientCreated(SkillObject obj, string id)
    {
    }

    public void OnClientDestroyed(SkillObject obj, string id)
    {
    }

    public void OnServerCreated(SkillObject obj, string id)
    {
    }

    public void OnServerDestroyed(SkillObject obj, string id)
    {
    }
}