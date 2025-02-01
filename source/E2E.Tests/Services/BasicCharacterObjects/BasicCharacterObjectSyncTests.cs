using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using TaleWorlds.Core;
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

            // Act
            string? characterId = null;
            string? cultureId = null;
            server.Call(() =>
            {
                BasicCharacterObject characterObject = new BasicCharacterObject();
                BasicCultureObject culture = new BasicCultureObject();

                Assert.True(server.ObjectManager.TryGetId(characterObject, out characterId));
                Assert.True(server.ObjectManager.TryGetId(culture, out cultureId));

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

            });

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject(characterId, out BasicCharacterObject clientCharacter));
                Assert.True(client.ObjectManager.TryGetObject(cultureId, out BasicCultureObject clientCulture));

                Assert.Equal(5, clientCharacter.Age);
                Assert.Equal("test", clientCharacter.BeardTags);
                Assert.Equal(FormationClass.Cavalry, clientCharacter.DefaultFormationClass);
                Assert.Equal(69, clientCharacter.DefaultFormationGroup);
                Assert.Equal(420, clientCharacter.DismountResistance);
                Assert.Equal(clientCulture, clientCharacter.Culture); //Basic Culture Object lifetime
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
            }
        }
    }
}
