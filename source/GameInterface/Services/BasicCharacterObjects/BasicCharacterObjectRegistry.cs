using GameInterface.Services.Registry;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCharacterObjects
{
    internal class BasicCharacterObjectRegistry : RegistryBase<BasicCharacterObject>
    {
        private const string IdPrefix = "CoopBasicCharacter";
        private static int InstanceCounter = 0;

        public BasicCharacterObjectRegistry(IRegistryCollection collection) : base(collection)
        {
        }

        public override void RegisterAll()
        {
            foreach (BasicCharacterObject character in Campaign.Current.Characters)
            {
                if (TryGetId(character, out _)) continue;

                RegisterExistingObject(character.StringId, character);
            }
        }

        protected override string GetNewId(BasicCharacterObject obj)
        {
            return $"{IdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        }
    }
}
