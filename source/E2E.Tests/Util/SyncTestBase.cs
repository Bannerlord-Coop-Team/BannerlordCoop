using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Util
{
    public abstract class SyncTestBase : IDisposable
    {
        internal E2ETestEnvironment TestEnvironment { get; }

        internal EnvironmentInstance Server => TestEnvironment.Server;

        internal IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        public SyncTestBase(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        /// <summary>
        /// Registers a player with its own party on every instance. The client plays it as its main party and the server
        /// links its peer, or with no client the player is registered offline.
        /// </summary>
        internal Player CreatePlayer(EnvironmentInstance? client, string controllerId)
        {
            var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
            Player player = null!;
            Server.Call(() =>
            {
                var hero = Server.GetRegisteredObject<MobileParty>(partyId).LeaderHero;
                Assert.True(Server.ObjectManager.TryGetId(hero, out var heroId));
                Assert.True(Server.ObjectManager.TryGetId(hero.Clan, out var clanId));
                Assert.True(Server.ObjectManager.TryGetId(hero.CharacterObject, out var characterId));
                player = new Player(controllerId, heroId, partyId, clanId, characterId);
            });
            foreach (var instance in Clients.Prepend(Server))
            {
                instance.Call(() =>
                {
                    using var scope = new AllowedThread();
                    var hero = instance.GetRegisteredObject<Hero>(player.HeroId);
                    hero.Clan._banner = new Banner();
                    Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(new Player(
                        controllerId, player.HeroId, partyId, player.ClanId, player.CharacterObjectId)));
                    if (instance == client)
                    {
                        instance.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
                        Game.Current.PlayerTroop = hero.CharacterObject;
                        Campaign.Current.MainParty = instance.GetRegisteredObject<MobileParty>(partyId);
                        Campaign.Current.PlayerDefaultFaction = hero.Clan;
                        Campaign.Current.PlayerEncounter = null;
                    }
                });
            }
            if (client != null)
                Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer(controllerId, client.NetPeer));
            return player;
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }
    }
}
