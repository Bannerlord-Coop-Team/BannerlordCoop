using Common.Util;
using E2E.Tests.Util;
using GameInterface.Services.TroopRosters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.TroopRosters
{
    /// <summary>
    /// End to end tests for the whole-roster snapshot sync handled by
    /// <see cref="GameInterface.Services.TroopRosters.Handlers.TroopRosterSnapshotHandler"/>.
    /// </summary>
    /// <remarks>
    /// Each test drives an authoritative <see cref="TroopRoster"/> mutation on the server. The server
    /// patch publishes the change event (e.g. CountsAtIndexAdded), which marks the roster dirty in the
    /// server's <see cref="TroopRosterSyncCoalescer"/>. Flushing the coalescer (the per-frame step the
    /// main loop performs in game) sends one whole-roster snapshot to every client, which rebuilds the
    /// roster from it. The client therefore mirrors the full server roster rather than replaying a
    /// per-index delta, so it never indexes past an under-populated array.
    /// </remarks>
    public class TroopRosterSnapshotHandlerTests : SyncTestBase
    {
        private readonly string TroopRosterId;
        private readonly string CharacterId1;
        private readonly string CharacterId2;

        public TroopRosterSnapshotHandlerTests(ITestOutputHelper output) : base(output)
        {
            TroopRosterId = TestEnvironment.CreateRegisteredObject<TroopRoster>();
            CharacterId1 = TestEnvironment.CreateRegisteredObject<CharacterObject>();
            CharacterId2 = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        }

        // Note: a bare AddNewElement (a 0-count slot) is intentionally not covered — the snapshot is
        // character+count based and UpdateWithData rebuilds via AddToCounts, which (like the game's own
        // RemoveZeroCounts) drops 0-count elements. Adding a brand new troop with a count is covered by
        // Server_AddToCounts_NewTroop_SyncsToClients below.

        [Fact]
        public void Server_AddToCountsAtIndex_SyncsToClients()
        {
            SeedTroopOnServer(CharacterId1, count: 5);

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));

                roster.AddToCountsAtIndex(0, countChange: 3, woundedCountChange: 0, xpChange: 0, removeDepleted: false);
            });

            PumpCoalescer();

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(1, roster.Count);
                Assert.Equal(8, roster.GetElementCopyAtIndex(0).Number);
            }
        }

        [Fact]
        public void Server_RemoveZeroCounts_SyncsToClients()
        {
            // A zero-count element (CharacterId1) and a populated element (CharacterId2) on the server.
            SeedTroopOnServer(CharacterId1, count: 0);
            SeedTroopOnServer(CharacterId2, count: 3);

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(2, roster.Count);

                roster.RemoveZeroCounts();
            });

            PumpCoalescer();

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(CharacterId2, out var remaining));

                Assert.Equal(1, roster.Count);
                Assert.Same(remaining, roster.GetElementCopyAtIndex(0).Character);
            }
        }

        [Fact]
        public void Server_SetElementNumber_SyncsToClients()
        {
            SeedTroopOnServer(CharacterId1, count: 5);

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));

                roster.SetElementNumber(0, 10);
            });

            PumpCoalescer();

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(10, roster.GetElementCopyAtIndex(0).Number);
            }
        }

        [Fact]
        public void Server_SetElementWoundedNumber_SyncsToClients()
        {
            SeedTroopOnServer(CharacterId1, count: 5);

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));

                roster.SetElementWoundedNumber(0, 2);
            });

            PumpCoalescer();

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(2, roster.GetElementCopyAtIndex(0).WoundedNumber);
            }
        }

        [Fact]
        public void Server_SetElementXp_SyncsToClients()
        {
            SeedTroopOnServer(CharacterId1, count: 5);

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));

                roster.SetElementXp(0, 100);
            });

            PumpCoalescer();

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(100, roster.GetElementCopyAtIndex(0).Xp);
            }
        }

        // The following exercise the higher-level public mutators, which delegate to the patched
        // primitives. A single snapshot reflects the resulting full roster on every client.

        [Fact]
        public void Server_AddToCounts_NewTroop_SyncsToClients()
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));

                roster.AddToCounts(character, 5);
            });

            PumpCoalescer();

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));

                Assert.Equal(1, roster.Count);
                Assert.Same(character, roster.GetElementCopyAtIndex(0).Character);
                Assert.Equal(5, roster.GetElementCopyAtIndex(0).Number);
            }
        }

        [Fact]
        public void Server_Clear_SyncsToClients()
        {
            SeedTroopOnServer(CharacterId1, count: 3);
            SeedTroopOnServer(CharacterId2, count: 4);

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(2, roster.Count);

                roster.Clear();
            });

            PumpCoalescer();

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(0, roster.Count);
                Assert.Equal(0, roster.TotalManCount);
            }
        }

        [Fact]
        public void Server_WoundTroop_SyncsToClients()
        {
            SeedTroopOnServer(CharacterId1, count: 5);

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));

                roster.WoundTroop(character, 2, default);
            });

            PumpCoalescer();

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(5, roster.GetElementCopyAtIndex(0).Number);
                Assert.Equal(2, roster.GetElementCopyAtIndex(0).WoundedNumber);
            }
        }

        [Fact]
        public void Server_ManyChangesToOneRoster_CollapseIntoOneSnapshot()
        {
            // Several mutations to the same roster in one frame must collapse into a single snapshot
            // that carries the final state, rather than one snapshot per change.
            SeedTroopOnServer(CharacterId1, count: 5);

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(CharacterId2, out var character2));

                roster.AddToCountsAtIndex(0, countChange: 3, woundedCountChange: 0, xpChange: 0, removeDepleted: false);
                roster.AddToCounts(character2, 2);
                roster.SetElementWoundedNumber(0, 1);
            });

            PumpCoalescer();

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character1));
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(CharacterId2, out var character2));

                Assert.Equal(2, roster.Count);

                var firstIndex = roster.FindIndexOfTroop(character1);
                var secondIndex = roster.FindIndexOfTroop(character2);
                Assert.True(firstIndex >= 0);
                Assert.True(secondIndex >= 0);

                Assert.Equal(8, roster.GetElementCopyAtIndex(firstIndex).Number);
                Assert.Equal(1, roster.GetElementCopyAtIndex(firstIndex).WoundedNumber);
                Assert.Equal(2, roster.GetElementCopyAtIndex(secondIndex).Number);
            }
        }

        [Fact]
        public void Server_HeroServingInRoster_SyncsToClients()
        {
            // A hero serving in a roster is packed by its Hero id (IsHero = true) and rebuilt on the client
            // by resolving the Hero and taking its CharacterObject, rather than being treated as a basic
            // troop. This exercises the hero branch of the pack/apply that the basic-troop tests above do not.
            string heroId = TestEnvironment.CreateRegisteredObject<Hero>();

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

                roster.AddToCounts(hero.CharacterObject, 1);
            });

            PumpCoalescer();

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

                Assert.Equal(1, roster.Count);
                Assert.True(roster.Contains(hero.CharacterObject));
                Assert.Equal(1, roster.GetElementCopyAtIndex(0).Number);
            }
        }

        [Fact]
        public void Server_HeroAndBasicTroopInRoster_SyncToClients()
        {
            // One snapshot carrying both a hero (packed by Hero id) and a basic troop (packed by
            // CharacterObject id) rebuilds both on the client, exercising both branches of the discriminator
            // in a single apply.
            string heroId = TestEnvironment.CreateRegisteredObject<Hero>();
            SeedTroopOnServer(CharacterId1, count: 4);

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

                roster.AddToCounts(hero.CharacterObject, 1);
            });

            PumpCoalescer();

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var troop));

                Assert.Equal(2, roster.Count);

                var heroIndex = roster.FindIndexOfTroop(hero.CharacterObject);
                var troopIndex = roster.FindIndexOfTroop(troop);
                Assert.True(heroIndex >= 0);
                Assert.True(troopIndex >= 0);
                Assert.Equal(1, roster.GetElementCopyAtIndex(heroIndex).Number);
                Assert.Equal(4, roster.GetElementCopyAtIndex(troopIndex).Number);
            }
        }

        #region Helpers
        /// <summary>
        /// Pumps the server's snapshot coalescer, the step the game's main loop performs once per
        /// frame, sending a whole-roster snapshot to every client.
        /// </summary>
        private void PumpCoalescer()
        {
            Server.Call(() => Server.Resolve<TroopRosterSyncCoalescer>().Update(TimeSpan.Zero));
        }

        /// <summary>
        /// Adds <paramref name="count"/> of <paramref name="characterId"/> to the roster on the server
        /// only, without triggering sync, so the server starts from a known state. A <paramref name="count"/>
        /// of zero seeds an empty (zero-count) element. Clients receive the state through the snapshot
        /// rather than being seeded directly. <see cref="AllowedThread"/> suppresses the authority patches.
        /// </summary>
        private void SeedTroopOnServer(string characterId, int count)
        {
            Server.Call(() =>
            {
                using (new AllowedThread())
                {
                    Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                    Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));

                    // Seed via AddNewElement + AddToCountsAtIndex, which AllowedThread suppresses.
                    // AddToCounts must not be used here: TroopRosterAddToCountsPatch intentionally
                    // publishes sync for AddToCounts even on an allowed thread (the recruitment flow
                    // runs under AllowedThread), so seeding through it would sync the seed too.
                    roster.AddNewElement(character, -1);
                    if (count > 0)
                    {
                        var index = roster.FindIndexOfTroop(character);
                        roster.AddToCountsAtIndex(index, count, woundedCountChange: 0, xpChange: 0, removeDepleted: false);
                    }
                }
            });
        }
        #endregion
    }
}
