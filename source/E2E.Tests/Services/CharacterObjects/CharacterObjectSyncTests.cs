using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using static E2E.Tests.Environment.E2ETestEnvironment;

namespace E2E.Tests.Services.CharacterObjects
{
    public class CharacterObjectSyncTests : SyncTestBase
    {
        private readonly string CharacterObjectId;

        public CharacterObjectSyncTests(ITestOutputHelper output) : base(output)
        {
            CharacterObjectId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
            TestEnvironment.CreateRegisteredObject<Hero>();
            TestEnvironment.CreateRegisteredObject<TraitObject>();
        }
        [Fact]
        public void Server_CharacterObject_Properties()
        {
            var assertHelper = TestEnvironment.CreateAssertHelper<CharacterObject>(CharacterObjectId);
            //Arrange
            assertHelper.AssertProperty<CharacterObject, bool>(nameof(CharacterObject.HiddenInEncyclopedia), true);
            assertHelper.AssertReferenceProperty<CharacterObject, Hero>(nameof(CharacterObject.HeroObject));

        }
        [Fact]
        public void Server_CharacterObject_Fields()
        {
            var assertHelper = TestEnvironment.CreateAssertHelper<CharacterObject>(CharacterObjectId);

            //Arrange
            assertHelper.AssertField<CharacterObject, CharacterRestrictionFlags>(nameof(CharacterObject._characterRestrictionFlags), CharacterRestrictionFlags.NotTransferableInPartyScreen);
            assertHelper.AssertReferenceField<CharacterObject, CharacterObject>(nameof(CharacterObject._originCharacter));
            assertHelper.AssertReferenceField<CharacterObject, TraitObject>(nameof(CharacterObject._persona));
            assertHelper.AssertPropertyOwnerField<CharacterObject, TraitObject>(nameof(CharacterObject._characterTraits));
            assertHelper.AssertReferenceField<CharacterObject, CharacterObject>(nameof(CharacterObject._civilianEquipmentTemplate));
            assertHelper.AssertReferenceField<CharacterObject, CharacterObject>(nameof(CharacterObject._battleEquipmentTemplate));
            assertHelper.AssertField<CharacterObject, Occupation>(nameof(CharacterObject._occupation), Occupation.Tavernkeeper);



        }
    }
}
