using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BasicCharacterObjects
{
    public class BasicCharacterObjectSyncTests
    {
        E2ETestEnvironment TestEnvironment { get; }

        EnvironmentInstance Server => TestEnvironment.Server;

        IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        public BasicCharacterObjectSyncTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        [Fact]
        public void ServerBasicCharacterObject_SyncAll()
        {
            // Arrange
            var server = TestEnvironment.Server;

            var basicHeroField = AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject._isBasicHero));
            var mountedField = AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject._isMounted));
            var rangedField = AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject._isRanged));
            var rosterField = AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject._equipmentRoster));
            var skillField = AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject.DefaultCharacterSkills));
            var nameField = AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject._basicName));
            // Get field intercept to use on the server to simulate the field changing
            var heroIntercept = TestEnvironment.GetIntercept(basicHeroField);
            var mountedIntercept = TestEnvironment.GetIntercept(mountedField);
            var rangedIntercept = TestEnvironment.GetIntercept(rangedField);
            var rosterIntercept = TestEnvironment.GetIntercept(rosterField);
            var skillIntercept = TestEnvironment.GetIntercept(skillField);
            var nameIntercept = TestEnvironment.GetIntercept(nameField);

            // Act
            string? characterId = null;
            string? cultureId = null;
            string? skillId = null;
            string? equipmentRosterId = null;
            TextObject name = new TextObject("test");   
            server.Call(() =>
            {
                BasicCharacterObject characterObject = new BasicCharacterObject();
                BasicCultureObject culture = new BasicCultureObject();
                MBCharacterSkills skills = new MBCharacterSkills();
                MBEquipmentRoster equipmentRoster = new MBEquipmentRoster();

                Assert.True(server.ObjectManager.TryGetId(characterObject, out characterId));
                Assert.True(server.ObjectManager.TryGetId(culture, out cultureId));
                Assert.True(server.ObjectManager.TryGetId(skills, out skillId));
                Assert.True(server.ObjectManager.TryGetId(equipmentRoster, out equipmentRosterId));

                characterObject.Age = 5;
                characterObject.BeardTags = "test";
                characterObject.DefaultFormationClass = FormationClass.Cavalry;
                characterObject.DefaultFormationGroup = 69;
                characterObject.DismountResistance = 420;
                characterObject.Culture = culture; //Basic Culture Object
                characterObject.FaceDirtAmount = 42;
                characterObject.FaceMeshCache = true;
                characterObject.FormationPositionPreference = FormationPositionPreference.Middle;
                characterObject.HairTags = "test";
                characterObject.IsFemale = true;
                characterObject.IsObsolete = true;
                characterObject.IsSoldier = true;
                characterObject.KnockbackResistance = 165;
                characterObject.KnockdownResistance = 178;
                characterObject.Level = 66;
                characterObject.Race = 4;
                characterObject.TattooTags = "test";

                // Simulate the field changing
                heroIntercept.Invoke(null, new object[] { characterObject, true });
                mountedIntercept.Invoke(null, new object[] { characterObject, true });
                rangedIntercept.Invoke(null, new object[] { characterObject, true });
                rosterIntercept.Invoke(null, new object[] { characterObject, equipmentRoster });
                skillIntercept.Invoke(null, new object[] { characterObject, skills });
                nameIntercept.Invoke(null, new object[] { characterObject, name });
            });

            Assert.True(server.ObjectManager.TryGetObject(skillId, out MBCharacterSkills serverSkills));
            Assert.True(server.ObjectManager.TryGetObject(equipmentRosterId, out MBEquipmentRoster serverEquipmentRoster));
            Assert.True(server.ObjectManager.TryGetObject(cultureId, out BasicCultureObject serverCulture));

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject(characterId, out BasicCharacterObject clientCharacter));
                Assert.True(client.ObjectManager.TryGetObject(cultureId, out BasicCultureObject clientCulture));

                Assert.Equal(5, clientCharacter.Age);
                Assert.Equal("test", clientCharacter.BeardTags);
                Assert.Equal(FormationClass.Cavalry, clientCharacter.DefaultFormationClass);
                Assert.Equal(69, clientCharacter.DefaultFormationGroup);
                Assert.Equal(420, clientCharacter.DismountResistance);
                Assert.Equal(serverCulture.StringId, clientCharacter.Culture.StringId); //Basic Culture Object lifetime
                Assert.Equal(42, clientCharacter.FaceDirtAmount);
                Assert.True(clientCharacter.FaceMeshCache);
                Assert.Equal(FormationPositionPreference.Middle, clientCharacter.FormationPositionPreference);
                Assert.Equal("test", clientCharacter.HairTags);
                Assert.True(clientCharacter.IsFemale);
                Assert.True(clientCharacter.IsObsolete);
                Assert.True(clientCharacter.IsSoldier);
                Assert.Equal(165, clientCharacter.KnockbackResistance);
                Assert.Equal(178, clientCharacter.KnockdownResistance);
                Assert.Equal(66, clientCharacter.Level);
                Assert.Equal(4, clientCharacter.Race);
                Assert.Equal("test", clientCharacter.TattooTags);

                Assert.True(clientCharacter._isBasicHero);
                Assert.True(clientCharacter._isMounted);
                Assert.True(clientCharacter._isRanged);
                Assert.Equal(serverEquipmentRoster.StringId, clientCharacter._equipmentRoster.StringId); 
                Assert.Equal(serverSkills.StringId, clientCharacter.DefaultCharacterSkills.StringId);
                Assert.True(name.Equals(clientCharacter._basicName));
            }
        }
    }
}
