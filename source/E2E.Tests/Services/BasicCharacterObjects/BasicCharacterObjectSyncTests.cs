using E2E.Tests.Util;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BasicCharacterObjects
{
    public class BasicCharacterObjectSyncTests : SyncTestBase
    {
        public BasicCharacterObjectSyncTests(ITestOutputHelper output) : base(output)
        {
            TestEnvironment.CreateRegisteredObject<BasicCharacterObject>();
            TestEnvironment.CreateRegisteredObject<BasicCultureObject>();
            TestEnvironment.CreateRegisteredObject<MBCharacterSkills>();
            TestEnvironment.CreateRegisteredObject<MBEquipmentRoster>();
        }

        [Fact]
        public void Server_BasicCharacterObject_Properties()
        {
            // Arrange
            TestEnvironment.AssertProperty<BasicCharacterObject, float>(nameof(BasicCharacterObject.Age), 5f);
            //TestEnvironment.AssertProperty<BasicCharacterObject, string>(nameof(BasicCharacterObject.BeardTags), "test", "");
            TestEnvironment.AssertProperty<BasicCharacterObject, FormationClass>(nameof(BasicCharacterObject.DefaultFormationClass), FormationClass.Cavalry);
            TestEnvironment.AssertProperty<BasicCharacterObject, int>(nameof(BasicCharacterObject.DefaultFormationGroup), 69);
            TestEnvironment.AssertProperty<BasicCharacterObject, float>(nameof(BasicCharacterObject.DismountResistance), 420f);
            TestEnvironment.AssertProperty<BasicCharacterObject, float>(nameof(BasicCharacterObject.FaceDirtAmount), 42f);
            TestEnvironment.AssertProperty<BasicCharacterObject, bool>(nameof(BasicCharacterObject.FaceMeshCache), true);
            TestEnvironment.AssertProperty<BasicCharacterObject, FormationPositionPreference>(nameof(BasicCharacterObject.FormationPositionPreference), FormationPositionPreference.Middle);
            //TestEnvironment.AssertProperty<BasicCharacterObject, string>(nameof(BasicCharacterObject.HairTags), "test", "");
            TestEnvironment.AssertProperty<BasicCharacterObject, bool>(nameof(BasicCharacterObject.IsFemale), true);
            TestEnvironment.AssertProperty<BasicCharacterObject, bool>(nameof(BasicCharacterObject.IsObsolete), true);
            TestEnvironment.AssertProperty<BasicCharacterObject, bool>(nameof(BasicCharacterObject.IsSoldier), true);
            TestEnvironment.AssertProperty<BasicCharacterObject, float>(nameof(BasicCharacterObject.KnockbackResistance), 165f);
            TestEnvironment.AssertProperty<BasicCharacterObject, float>(nameof(BasicCharacterObject.KnockdownResistance), 178f);
            TestEnvironment.AssertProperty<BasicCharacterObject, int>(nameof(BasicCharacterObject.Level), 66);
            TestEnvironment.AssertProperty<BasicCharacterObject, int>(nameof(BasicCharacterObject.Race), 4);
            //TestEnvironment.AssertProperty<BasicCharacterObject, string>(nameof(BasicCharacterObject.TattooTags), "test", "");

            TestEnvironment.AssertReferenceProperty<BasicCharacterObject, BasicCultureObject>(nameof(BasicCharacterObject.Culture));
        }

        [Fact]
        public void Server_BasicCharacterObject_Fields()
        {
            // Arrange
            TestEnvironment.AssertField<BasicCharacterObject, bool>(nameof(BasicCharacterObject._isBasicHero), true);
            TestEnvironment.AssertField<BasicCharacterObject, bool>(nameof(BasicCharacterObject._isMounted), true);
            TestEnvironment.AssertField<BasicCharacterObject, bool>(nameof(BasicCharacterObject._isRanged),true);
            TestEnvironment.AssertField<BasicCharacterObject, TextObject>(nameof(BasicCharacterObject._basicName), new TextObject("test"));

            TestEnvironment.AssertReferenceField<BasicCharacterObject, MBEquipmentRoster>(nameof(BasicCharacterObject._equipmentRoster));
            TestEnvironment.AssertReferenceField<BasicCharacterObject, MBCharacterSkills>(nameof(BasicCharacterObject.DefaultCharacterSkills));
        }
    }
}
