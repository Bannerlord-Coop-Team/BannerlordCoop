using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Buildings
{
    public class CharacterObjectSyncTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }

        EnvironmentInstance Server => TestEnvironment.Server;

        IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        private readonly string CharacterId;
        private readonly string HeroId;
        private Occupation newOccupation = Occupation.Armorer;
        private CharacterObject newCharacterObject = new CharacterObject();
        private CharacterTraits newCharacterTraits = new CharacterTraits();
        private TraitObject newTraitObject = new TraitObject("testObject");
        private CharacterRestrictionFlags newRestrictionFlags = CharacterRestrictionFlags.CanNotGoInHideout;

        public CharacterObjectSyncTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);

            CharacterObject characterObject = new CharacterObject();
            Hero hero = new Hero();

            // Create objects on the server
            Assert.True(Server.ObjectManager.AddNewObject(characterObject, out CharacterId));
            Assert.True(Server.ObjectManager.AddNewObject(hero, out HeroId));

            // Create objects on all clients
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.AddExisting(CharacterId, characterObject));
                Assert.True(client.ObjectManager.AddExisting(HeroId, hero));
            }
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void Server_CharacterObject_Sync()
        {
            // Arrange
            var server = TestEnvironment.Server;

            var occupationField = AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._occupation));
            var battleEquipmentTemplateField = AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._battleEquipmentTemplate));
            var civilianEquipmentTemplateField = AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._civilianEquipmentTemplate));
            var characterTraitsField = AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._characterTraits));
            var personaField = AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._persona));
            var originCharacterField = AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._originCharacter));
            var characterRestrictionField = AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._characterRestrictionFlags));

            // Get field intercept to use on the server to simulate the field changing
            var occupationIntercept = TestEnvironment.GetIntercept(occupationField);
            var battleEquipmentTemplateIntercept = TestEnvironment.GetIntercept(battleEquipmentTemplateField);
            var civilianEquipmentTemplateIntercept = TestEnvironment.GetIntercept(civilianEquipmentTemplateField);
            var characterTraitsIntercept = TestEnvironment.GetIntercept(characterTraitsField);
            var personaIntercept = TestEnvironment.GetIntercept(personaField);
            var originCharacterIntercept = TestEnvironment.GetIntercept(originCharacterField);
            var characterRestrictionIntercept = TestEnvironment.GetIntercept(characterRestrictionField);

            // Act
            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(CharacterId, out var serverCharacter));
                Assert.True(server.ObjectManager.TryGetObject<Hero>(HeroId, out var serverHero));

                // Simulate the field changing
                occupationIntercept.Invoke(null, new object[] { serverCharacter, newOccupation });
                battleEquipmentTemplateIntercept.Invoke(null, new object[] { serverCharacter, newCharacterObject });
                civilianEquipmentTemplateIntercept.Invoke(null, new object[] { serverCharacter, newCharacterObject });
                characterTraitsIntercept.Invoke(null, new object[] { serverCharacter, newCharacterTraits });
                personaIntercept.Invoke(null, new object[] { serverCharacter, newTraitObject });
                originCharacterIntercept.Invoke(null, new object[] { serverCharacter, newCharacterObject });
                characterRestrictionIntercept.Invoke(null, new object[] { serverCharacter, newRestrictionFlags });

                serverCharacter.HiddenInEncylopedia = true;
                serverCharacter.HeroObject = serverHero;

                Assert.Equal(newOccupation, serverCharacter.Occupation);
                Assert.Equal(newCharacterObject, serverCharacter._battleEquipmentTemplate);
                Assert.Equal(newCharacterObject, serverCharacter._civilianEquipmentTemplate);
                Assert.Equal(newCharacterTraits, serverCharacter._characterTraits);
                Assert.Equal(newTraitObject, serverCharacter._persona);
                Assert.Equal(newCharacterObject, serverCharacter._originCharacter);
                Assert.Equal(newRestrictionFlags, serverCharacter._characterRestrictionFlags);

                Assert.True(serverCharacter.HiddenInEncylopedia);
                Assert.Equal(serverHero, serverCharacter.HeroObject);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(CharacterId, out var clientCharacter));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(HeroId, out var clientHero));

                Assert.Equal(newOccupation, clientCharacter.Occupation);
                Assert.Equal(newCharacterObject, clientCharacter._battleEquipmentTemplate);
                Assert.Equal(newCharacterObject, clientCharacter._civilianEquipmentTemplate);
                Assert.Equal(newCharacterTraits, clientCharacter._characterTraits);
                Assert.Equal(newTraitObject, clientCharacter._persona);
                Assert.Equal(newCharacterObject, clientCharacter._originCharacter);
                Assert.Equal(newRestrictionFlags, clientCharacter._characterRestrictionFlags);

                Assert.True(clientCharacter.HiddenInEncylopedia);
                Assert.Equal(clientHero, clientCharacter.HeroObject);
            }
        }
    }
}
