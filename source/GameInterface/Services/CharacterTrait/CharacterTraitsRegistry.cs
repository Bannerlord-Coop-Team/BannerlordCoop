using GameInterface.Services.Registry;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.CharacterTrait
{
    internal class CharacterTraitsRegistry : RegistryBase<CharacterTraits>
    {
        private const string TraitIdPrefix = "CoopCharacterTrait";
        private static int InstanceCounter = 0;

        public CharacterTraitsRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            foreach (CharacterObject character in Campaign.Current.Characters)
            {
                CharacterTraits traits = character._characterTraits;

                if (RegisterNewObject(traits, out var _) == false)
                {
                    Logger.Error($"Unable to register {traits}");
                }
            }
        }

        protected override string GetNewId(CharacterTraits obj)
        {
            return $"{TraitIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        }
    }
}
