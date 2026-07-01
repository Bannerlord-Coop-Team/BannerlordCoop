using System;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.TroopRosters.Messages;
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
    /// identity from the server roster and sends an identity-keyed message, which the client applies
    /// through the same vanilla mutator, found by character. The client roster starts empty, so the
    /// AddToCounts tests also prove a positive add creates the element (with correct cached totals) on an
    /// under-populated client; the Set tests seed the element first, since an absolute Set for a troop the
    /// client does not have is skipped rather than creating a totals-corrupting placeholder.
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
                // The create path must keep the cached totals correct (AddNewElement alone would not).
                Assert.Equal(5, roster.TotalManCount);
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
                Assert.Equal(7, roster.TotalManCount);
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
                Assert.Equal(1, roster.GetElementCopyAtIndex(roster.FindIndexOfTroop(hero.CharacterObject)).Number);
                Assert.Equal(1, roster.TotalHeroes);
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

        [Fact]
        public void Client_OverSubtractRemove_ClampsToZero_NeverGoesNegative()
        {
            // The authority adds 2 of a troop; the client receives them.
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character, CharacterId1);
                roster.AddToCounts(character, 2);
            });

            var client = Clients.First();

            // An over-subtract lands on the client: a remove of -5 against an element of 2 would drive the count
            // to -3. In the live bug this is a DUPLICATE / double-counted remove from the authority (e.g. a battle
            // casualty/capture sending the same remove twice, or a host-migration replay). The handler's guard
            // must clamp to zero - apply only what is actually there - so the roster can never go negative, and
            // log an error naming the over-subtract. The guard is a safety net; the real fix is upstream not
            // sending the remove twice.
            client.Call(() =>
            {
                var broker = client.Resolve<IMessageBroker>();
                broker.Publish(this, new NetworkTroopRosterAddCounts(TroopRosterId, CharacterId1, -5, 0, 0, false));
            });

            client.Call(() =>
            {
                Resolve(client, out var roster, out var character, CharacterId1);
                int index = roster.FindIndexOfTroop(character);
                int number = index >= 0 ? roster.GetElementCopyAtIndex(index).Number : 0;
                Assert.Equal(0, number);
                Assert.True(roster.TotalManCount >= 0, $"roster TotalManCount went negative: {roster.TotalManCount}");
            });
        }

        private void Resolve(EnvironmentInstance instance, out TroopRoster roster, out CharacterObject character, string characterId)
        {
            Assert.True(instance.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out roster));
            Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(characterId, out character));
        }
    }
}
