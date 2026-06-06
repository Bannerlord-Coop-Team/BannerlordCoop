using GameInterface.Registry;
using System.Linq;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CharacterSkills
{
    internal class CharacterSkillsRegistry : RegistryBase<MBCharacterSkills>
    {
        private const string IdPrefix = "CoopCharacterSkills";
        private static int InstanceCounter = 0;

        public CharacterSkillsRegistry(IRegistryCollection collection) : base(collection)
        {
        }

        public override void RegisterAll()
        {
            var objectManager = MBObjectManager.Instance;

            if (objectManager == null)
            {
                Logger.Error("Unable to register objects when CampaignObjectManager is null");
                return;
            }

            foreach (var skill in objectManager.GetObjectTypeList<MBCharacterSkills>())
            {
                RegisterExistingObject(skill.StringId, skill);
            }
        }

        protected override string GetNewId(MBCharacterSkills obj)
        {
            return $"{IdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        }
    }
}
