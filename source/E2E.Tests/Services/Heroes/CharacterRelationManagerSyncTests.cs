using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes
{
    public class CharacterRelationManagerSyncTests : SyncTestBase
    {
        private readonly string Hero1Id;
        private readonly string Hero2Id;

        public CharacterRelationManagerSyncTests(ITestOutputHelper output) : base(output)
        {
            Hero1Id = TestEnvironment.CreateRegisteredObject<Hero>();
            Hero2Id = TestEnvironment.CreateRegisteredObject<Hero>();
        }

        [Fact]
        public void Server_SetHeroRelation_SyncsToClients()
        {
            // SetHeroRelation funnels every relation write; the server postfix publishes it and clients re-apply.
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject(Hero1Id, out Hero hero1));
                Assert.True(Server.ObjectManager.TryGetObject(Hero2Id, out Hero hero2));
                CharacterRelationManager.SetHeroRelation(hero1, hero2, 42);
            });

            foreach (var client in Clients)
            {
                client.Call(() =>
                {
                    Assert.True(client.ObjectManager.TryGetObject(Hero1Id, out Hero hero1));
                    Assert.True(client.ObjectManager.TryGetObject(Hero2Id, out Hero hero2));
                    Assert.Equal(42, CharacterRelationManager.GetHeroRelation(hero1, hero2));
                });
            }
        }
    }
}
