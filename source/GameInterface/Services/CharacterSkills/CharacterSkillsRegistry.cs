using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CharacterSkills
{
    internal class CharacterSkillsRegistry : IAutoRegistry<MBCharacterSkills>
    {
        public IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(MBCharacterSkills));

        public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

        public ILogger Logger { get; }

        public CharacterSkillsRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
        {
            Logger = logger;

            autoRegistryFactory.RegisterType(this);
        }

        public void RegisterAllObjects(IRegistry<MBCharacterSkills> registry)
        {
            var mbObjectManager = MBObjectManager.Instance;

            if (mbObjectManager == null)
            {
                Logger.Error("Unable to register objects when CampaignObjectManager is null");
                return;
            }

            foreach (var skill in mbObjectManager.GetObjectTypeList<MBCharacterSkills>())
            {
                registry.RegisterExistingObject(skill.StringId, skill);
            }
        }

        public void OnClientCreated(MBCharacterSkills obj, string id)
        {
        }

        public void OnClientDestroyed(MBCharacterSkills obj, string id)
        {
        }

        public void OnServerCreated(MBCharacterSkills obj, string id)
        {
        }

        public void OnServerDestroyed(MBCharacterSkills obj, string id)
        {
        }
    }
}
