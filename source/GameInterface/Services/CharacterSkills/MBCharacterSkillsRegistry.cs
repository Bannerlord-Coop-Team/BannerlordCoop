using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CharacterSkills;

internal class MBCharacterSkillsRegistry : AutoRegistryBase<MBCharacterSkills>
{
    public MBCharacterSkillsRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(MBCharacterSkills));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var characterSkills in MBObjectManager.Instance.GetObjectTypeList<MBCharacterSkills>())
        {
            RegisterExistingObject(characterSkills.StringId, characterSkills);
        }
    }

    public override void OnClientCreated(MBCharacterSkills obj, string id)
    {
    }

    public override void OnClientDestroyed(MBCharacterSkills obj, string id)
    {
    }

    public override void OnServerCreated(MBCharacterSkills obj, string id)
    {
    }

    public override void OnServerDestroyed(MBCharacterSkills obj, string id)
    {
    }
}
