using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BasicCharacterObjects
{
    public class BasicCharacterObjectSyncTests : SyncTestBase
    {
        private readonly string CharacterObjectId;
        public BasicCharacterObjectSyncTests(ITestOutputHelper output) : base(output)
        {
            CharacterObjectId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        }

        [Fact]
        public void Server_BasicCharacterObject_Properties()
        {
            var assertHelper = TestEnvironment.CreateAssertHelper<CharacterObject>(CharacterObjectId);

            // Arrange
            assertHelper.AssertProperty<BasicCharacterObject, float>(nameof(BasicCharacterObject.Age), 5f);
            //TestEnvironment.AssertProperty<BasicCharacterObject, string>(nameof(BasicCharacterObject.BeardTags), "test", "");
            assertHelper.AssertProperty<BasicCharacterObject, FormationClass>(nameof(BasicCharacterObject.DefaultFormationClass), FormationClass.Cavalry);
            assertHelper.AssertProperty<BasicCharacterObject, int>(nameof(BasicCharacterObject.DefaultFormationGroup), 69);
            assertHelper.AssertProperty<BasicCharacterObject, float>(nameof(BasicCharacterObject.DismountResistance), 420f);
            assertHelper.AssertProperty<BasicCharacterObject, float>(nameof(BasicCharacterObject.FaceDirtAmount), 42f);
            assertHelper.AssertProperty<BasicCharacterObject, bool>(nameof(BasicCharacterObject.FaceMeshCache), true);
            assertHelper.AssertProperty<BasicCharacterObject, FormationPositionPreference>(nameof(BasicCharacterObject.FormationPositionPreference), FormationPositionPreference.Middle);
            //TestEnvironment.AssertProperty<BasicCharacterObject, string>(nameof(BasicCharacterObject.HairTags), "test", "");
            assertHelper.AssertProperty<BasicCharacterObject, bool>(nameof(BasicCharacterObject.IsFemale), true);
            assertHelper.AssertProperty<BasicCharacterObject, bool>(nameof(BasicCharacterObject.IsObsolete), true);
            assertHelper.AssertProperty<BasicCharacterObject, bool>(nameof(BasicCharacterObject.IsSoldier), true);
            assertHelper.AssertProperty<BasicCharacterObject, float>(nameof(BasicCharacterObject.KnockbackResistance), 165f);
            assertHelper.AssertProperty<BasicCharacterObject, float>(nameof(BasicCharacterObject.KnockdownResistance), 178f);
            assertHelper.AssertProperty<BasicCharacterObject, int>(nameof(BasicCharacterObject.Level), 66);
            assertHelper.AssertProperty<BasicCharacterObject, int>(nameof(BasicCharacterObject.Race), 4);
            //TestEnvironment.AssertProperty<BasicCharacterObject, string>(nameof(BasicCharacterObject.TattooTags), "test", "");

            assertHelper.AssertReferenceProperty<BasicCharacterObject, CultureObject>(nameof(BasicCharacterObject.Culture));
        }

        [Fact]
        public void Server_BasicCharacterObject_Fields()
        {
            var assertHelper = TestEnvironment.CreateAssertHelper<CharacterObject>(CharacterObjectId);

            // Arrange
            assertHelper.AssertField<BasicCharacterObject, bool>(nameof(BasicCharacterObject._isBasicHero), true);
            assertHelper.AssertField<BasicCharacterObject, bool>(nameof(BasicCharacterObject._isMounted), true);
            assertHelper.AssertField<BasicCharacterObject, bool>(nameof(BasicCharacterObject._isRanged),true);
            assertHelper.AssertField<BasicCharacterObject, TextObject>(nameof(BasicCharacterObject._basicName), new TextObject("test"));

            assertHelper.AssertReferenceField<BasicCharacterObject, MBEquipmentRoster>(nameof(BasicCharacterObject._equipmentRoster));
            assertHelper.AssertReferenceField<BasicCharacterObject, MBCharacterSkills>(nameof(BasicCharacterObject.DefaultCharacterSkills));
        }
    }
}
