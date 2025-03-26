using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.CharacterObjects
{
    public class CharacterObjectSyncTests : SyncTestBase
    {

        public CharacterObjectSyncTests(ITestOutputHelper output) : base(output)
        {
            TestEnvironment.CreateRegisteredObject<CharacterObject>();
            TestEnvironment.CreateRegisteredObject<Hero>();
            TestEnvironment.CreateRegisteredObject<TraitObject>();
            TestEnvironment.CreateRegisteredObject<CharacterTraits>();
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void Server_CharacterObject_Sync()
        {
            TestEnvironment.AssertProperty<CharacterObject, bool>(nameof(CharacterObject.HiddenInEncylopedia), true);
            TestEnvironment.AssertReferenceProperty<CharacterObject, Hero>(nameof(CharacterObject.HeroObject));

            TestEnvironment.AssertField<CharacterObject, Occupation>(nameof(CharacterObject._occupation), Occupation.Armorer);
            TestEnvironment.AssertReferenceField<CharacterObject, CharacterObject>(nameof(CharacterObject._battleEquipmentTemplate));
            TestEnvironment.AssertReferenceField<CharacterObject, CharacterObject>(nameof(CharacterObject._civilianEquipmentTemplate));
            TestEnvironment.AssertField<CharacterObject, CharacterRestrictionFlags>(nameof(CharacterObject._characterRestrictionFlags), CharacterRestrictionFlags.CanNotGoInHideout);
            TestEnvironment.AssertReferenceField<CharacterObject, CharacterTraits>(nameof(CharacterObject._characterTraits));
            TestEnvironment.AssertReferenceField<CharacterObject, TraitObject>(nameof(CharacterObject._persona));
            TestEnvironment.AssertReferenceField<CharacterObject, CharacterTraits>(nameof(CharacterObject._originCharacter));
        }
    }
}
