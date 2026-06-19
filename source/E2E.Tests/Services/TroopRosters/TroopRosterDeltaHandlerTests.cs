using System;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.TroopRosters
{
    /// <summary>
    /// End to end tests for the identity-keyed per-operation TroopRoster sync handled by
    /// <see cref="GameInterface.Services.TroopRosters.Handlers.TroopRosterDeltaHandler"/>.
    /// </summary>
    /// <remarks>
    /// Each test drives an authoritative <see cref="TroopRoster"/> mutation on the server. The server
    /// patch publishes a local event carrying the server index; the handler resolves the element's
    /// identity from the server roster and sends an identity-keyed message, which the client applies by
    /// finding-or-creating the element by character. The client roster starts empty, so these tests also
    /// prove the deltas self-heal an under-populated client roster without any whole-roster transfer.
    /// </remarks>
    public class TroopRosterDeltaHandlerTests : SyncTestBase
    {
        private readonly string TroopRosterId;
        private readonly string CharacterId1;
        private readonly string CharacterId2;

        public TroopRosterDeltaHandlerTests(ITestOutputHelper output) : base(output)
        {
            TroopRosterId = TestEnvironment.CreateRegisteredObject<TroopRoster>();
            CharacterId1 = TestEnvironment.CreateRegisteredObject<CharacterObject>();
            CharacterId2 = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        }

        [Fact]
        public void Server_AddToCounts_NewTroop_SyncsToClients()
        {
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character, CharacterId1);
                roster.AddToCounts(character, 5);
            });

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out var character, CharacterId1);
                Assert.Equal(1, roster.Count);
                Assert.Same(character, roster.GetElementCopyAtIndex(0).Character);
                Assert.Equal(5, roster.GetElementCopyAtIndex(0).Number);
            }
        }

        [Fact]
        public void Server_AddToCounts_WithWoundedAndXp_SyncsToClients()
        {
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character, CharacterId1);
                roster.AddToCounts(character, 5, woundedCount: 2, xpChange: 100);
            });

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out _, CharacterId1);
                var element = roster.GetElementCopyAtIndex(0);
                Assert.Equal(5, element.Number);
                Assert.Equal(2, element.WoundedNumber);
                Assert.Equal(100, element.Xp);
            }
        }

        [Fact]
        public void Server_AddToCounts_Subtract_SyncsToClients()
        {
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character, CharacterId1);
                roster.AddToCounts(character, 8);
                roster.AddToCounts(character, -3);
            });

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out _, CharacterId1);
                Assert.Equal(5, roster.GetElementCopyAtIndex(0).Number);
            }
        }

        [Fact]
        public void Server_SetElementNumber_SyncsToClients()
        {
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character, CharacterId1);
                roster.AddToCounts(character, 5);
                roster.SetElementNumber(roster.FindIndexOfTroop(character), 12);
            });

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out _, CharacterId1);
                Assert.Equal(12, roster.GetElementCopyAtIndex(0).Number);
            }
        }

        [Fact]
        public void Server_SetElementWoundedNumber_SyncsToClients()
        {
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character, CharacterId1);
                roster.AddToCounts(character, 5);
                roster.SetElementWoundedNumber(roster.FindIndexOfTroop(character), 3);
            });

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out _, CharacterId1);
                Assert.Equal(3, roster.GetElementCopyAtIndex(0).WoundedNumber);
            }
        }

        [Fact]
        public void Server_SetElementXp_SyncsToClients()
        {
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character, CharacterId1);
                roster.AddToCounts(character, 5);
                roster.SetElementXp(roster.FindIndexOfTroop(character), 250);
            });

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out _, CharacterId1);
                Assert.Equal(250, roster.GetElementCopyAtIndex(0).Xp);
            }
        }

        [Fact]
        public void Server_MultipleTroops_SyncToClients()
        {
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character1, CharacterId1);
                Resolve(Server, out _, out var character2, CharacterId2);
                roster.AddToCounts(character1, 3);
                roster.AddToCounts(character2, 4);
            });

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out var character1, CharacterId1);
                Resolve(client, out _, out var character2, CharacterId2);
                Assert.Equal(2, roster.Count);
                Assert.Equal(3, roster.GetElementCopyAtIndex(roster.FindIndexOfTroop(character1)).Number);
                Assert.Equal(4, roster.GetElementCopyAtIndex(roster.FindIndexOfTroop(character2)).Number);
            }
        }

        [Fact]
        public void Server_RemoveZeroCounts_SyncsToClients()
        {
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character1, CharacterId1);
                Resolve(Server, out _, out var character2, CharacterId2);
                roster.AddToCounts(character1, 3);
                roster.AddToCounts(character2, 4);
                // Zero character1 (without auto-removing it), then drop depleted elements.
                roster.SetElementNumber(roster.FindIndexOfTroop(character1), 0);
                roster.RemoveZeroCounts();
            });

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out var character2, CharacterId2);
                Assert.Equal(1, roster.Count);
                Assert.Same(character2, roster.GetElementCopyAtIndex(0).Character);
            }
        }

        [Fact]
        public void Server_HeroServingInRoster_SyncsToClients()
        {
            // A hero in the roster is keyed by its Hero id and rebuilt on the client via its CharacterObject.
            string heroId = TestEnvironment.CreateRegisteredObject<Hero>();

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                roster.AddToCounts(hero.CharacterObject, 1);
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.Equal(1, roster.Count);
                Assert.True(roster.Contains(hero.CharacterObject));
            }
        }

        [Fact]
        public void Server_SubtractToZeroInAllowedThread_RemovesOnClient()
        {
            // Capture/battle-finalize flows subtract-to-zero with removeDepleted while already inside an
            // AllowedThread, so only the AddToCounts postfix fires (the lower AddToCountsAtIndex patch
            // stands down). It must still replicate the removal by the character's identity, not a
            // now-stale post-removal index.
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character, CharacterId1);
                roster.AddToCounts(character, 5);
            });

            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character, CharacterId1);
                using (new AllowedThread())
                {
                    roster.AddToCounts(character, -5);
                }
            });

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out _, CharacterId1);
                Assert.Equal(0, roster.Count);
            }
        }

        private void Resolve(EnvironmentInstance instance, out TroopRoster roster, out CharacterObject character, string characterId)
        {
            Assert.True(instance.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out roster));
            Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(characterId, out character));
        }
    }
}
