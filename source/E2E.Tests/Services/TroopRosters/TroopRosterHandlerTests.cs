using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.TroopRosters
{
    /// <summary>
    /// End to end tests for every message handled by
    /// <see cref="GameInterface.Services.TroopRosters.Handlers.TroopRosterHandler"/>.
    /// </summary>
    /// <remarks>
    /// Each test drives an authoritative <see cref="TroopRoster"/> mutation on the server. The
    /// server patch publishes the authority event (e.g. CountsAtIndexAdded), the handler converts
    /// it to the network command (e.g. NetworkAddToCountsAtIndex) and sends it to every client,
    /// where the matching handler re-applies the mutation. A single round trip therefore exercises
    /// both messages of a pair.
    /// </remarks>
    public class TroopRosterHandlerTests : SyncTestBase
    {
        private readonly string TroopRosterId;
        private readonly string CharacterId1;
        private readonly string CharacterId2;

        public TroopRosterHandlerTests(ITestOutputHelper output) : base(output)
        {
            TroopRosterId = TestEnvironment.CreateRegisteredObject<TroopRoster>();
            CharacterId1 = TestEnvironment.CreateRegisteredObject<CharacterObject>();
            CharacterId2 = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        }

        #region AddNewElement: NewElementAdded -> NetworkAddNewElement
        [Fact]
        public void Server_AddNewElement_SyncsToClients()
        {
            // Act
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));

                roster.AddNewElement(character, -1);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));

                Assert.Equal(1, roster.Count);
                Assert.Same(character, roster.GetElementCopyAtIndex(0).Character);
            }
        }
        #endregion

        #region AddToCountsAtIndex: CountsAtIndexAdded -> NetworkAddToCountsAtIndex
        [Fact]
        public void Server_AddToCountsAtIndex_SyncsToClients()
        {
            // Arrange
            SeedTroopOnAll(CharacterId1, count: 5);

            // Act
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));

                roster.AddToCountsAtIndex(0, countChange: 3, woundedCountChange: 0, xpChange: 0, removeDepleted: false);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(8, roster.GetElementCopyAtIndex(0).Number);
            }
        }
        #endregion

        #region RemoveZeroCounts: ZeroCountsRemoved -> NetworkRemoveZeroCounts
        [Fact]
        public void Server_RemoveZeroCounts_SyncsToClients()
        {
            // Arrange: a zero-count element (CharacterId1) and a populated element (CharacterId2)
            SeedEmptyElementOnAll(CharacterId1);
            SeedTroopOnAll(CharacterId2, count: 3);

            // Act
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(2, roster.Count);

                roster.RemoveZeroCounts();
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(CharacterId2, out var remaining));

                Assert.Equal(1, roster.Count);
                Assert.Same(remaining, roster.GetElementCopyAtIndex(0).Character);
            }
        }
        #endregion

        #region SetElementNumber: ElementNumberSet -> NetworkSetElementNumber
        [Fact]
        public void Server_SetElementNumber_SyncsToClients()
        {
            // Arrange
            SeedTroopOnAll(CharacterId1, count: 5);

            // Act
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));

                roster.SetElementNumber(0, 10);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(10, roster.GetElementCopyAtIndex(0).Number);
            }
        }
        #endregion

        #region SetElementWoundedNumber: ElementWoundedNumberSet -> NetworkSetElementWoundedNumber
        [Fact]
        public void Server_SetElementWoundedNumber_SyncsToClients()
        {
            // Arrange
            SeedTroopOnAll(CharacterId1, count: 5);

            // Act
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));

                roster.SetElementWoundedNumber(0, 2);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(2, roster.GetElementCopyAtIndex(0).WoundedNumber);
            }
        }
        #endregion

        #region SetElementXp: ElementXpSet -> NetworkSetElementXp
        [Fact]
        public void Server_SetElementXp_SyncsToClients()
        {
            // Arrange
            SeedTroopOnAll(CharacterId1, count: 5);

            // Act
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));

                roster.SetElementXp(0, 100);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(100, roster.GetElementCopyAtIndex(0).Xp);
            }
        }
        #endregion

        #region ShiftTroopToIndex: TroopShiftedToIndex -> NetworkShiftTroopToIndex
        [Fact]
        public void Server_ShiftTroopToIndex_SyncsToClients()
        {
            // Arrange: CharacterId1 at index 0, CharacterId2 at index 1
            SeedTroopOnAll(CharacterId1, count: 3);
            SeedTroopOnAll(CharacterId2, count: 3);

            // Act
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));

                roster.ShiftTroopToIndex(0, 1);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character1));
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(CharacterId2, out var character2));

                Assert.Same(character2, roster.GetElementCopyAtIndex(0).Character);
                Assert.Same(character1, roster.GetElementCopyAtIndex(1).Character);
            }
        }
        #endregion

        #region SwapTroopsAtIndices: TroopsSwappedAtIndices -> NetworkSwapTroopsAtIndices
        [Fact]
        public void Server_SwapTroopsAtIndices_SyncsToClients()
        {
            // Arrange: CharacterId1 at index 0, CharacterId2 at index 1
            SeedTroopOnAll(CharacterId1, count: 3);
            SeedTroopOnAll(CharacterId2, count: 3);

            // Act
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));

                roster.SwapTroopsAtIndices(0, 1);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character1));
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(CharacterId2, out var character2));

                Assert.Same(character2, roster.GetElementCopyAtIndex(0).Character);
                Assert.Same(character1, roster.GetElementCopyAtIndex(1).Character);
            }
        }
        #endregion

        #region High-level routing: public mutators that delegate to the patched primitives
        // These exercise the higher-level TroopRoster API rather than the patched primitives
        // directly, locking in that they continue to route through synced primitives
        // (AddNewElement / AddToCountsAtIndex / SetElementXp / ...) rather than mutating the
        // backing data array. If a future game version bypasses the primitives, these regress.

        [Fact]
        public void Server_AddToCounts_NewTroop_SyncsToClients()
        {
            // Act: AddToCounts on an empty roster routes through AddNewElement + AddToCountsAtIndex
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));

                roster.AddToCounts(character, 5);
            });

            // Assert
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
        public void Server_AddToCounts_ExistingTroop_SyncsToClients()
        {
            // Arrange
            SeedTroopOnAll(CharacterId1, count: 5);

            // Act: AddToCounts on an existing element routes through AddToCountsAtIndex
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));

                roster.AddToCounts(character, 3);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(8, roster.GetElementCopyAtIndex(0).Number);
            }
        }

        [Fact]
        public void Server_Clear_SyncsToClients()
        {
            // Arrange: two populated elements
            SeedTroopOnAll(CharacterId1, count: 3);
            SeedTroopOnAll(CharacterId2, count: 4);

            // Act: Clear routes through AddToCountsAtIndex per element
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(2, roster.Count);

                roster.Clear();
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(0, roster.Count);
                Assert.Equal(0, roster.TotalManCount);
            }
        }

        [Fact]
        public void Server_RemoveTroop_SyncsToClients()
        {
            // Arrange
            SeedTroopOnAll(CharacterId1, count: 5);

            // Act: RemoveTroop routes through AddToCountsAtIndex (negative count)
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));

                roster.RemoveTroop(character, 2, default, 0);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(3, roster.GetElementCopyAtIndex(0).Number);
            }
        }

        [Fact]
        public void Server_AddXpToTroop_SyncsToClients()
        {
            // Arrange
            SeedTroopOnAll(CharacterId1, count: 5);

            // Act: AddXpToTroop routes through AddXpToTroopAtIndex -> SetElementXp
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));

                roster.AddXpToTroop(character, 100);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(100, roster.GetElementCopyAtIndex(0).Xp);
            }
        }

        [Fact]
        public void Server_WoundTroop_SyncsToClients()
        {
            // Arrange
            SeedTroopOnAll(CharacterId1, count: 5);

            // Act: WoundTroop routes through AddToCountsAtIndex (wounded count change)
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));

                roster.WoundTroop(character, 2, default);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(2, roster.GetElementCopyAtIndex(0).WoundedNumber);
            }
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Adds <paramref name="count"/> of <paramref name="characterId"/> to the roster on the
        /// server and every client without triggering sync, so each instance starts from the same
        /// known state. <see cref="AllowedThread"/> suppresses the authority patches.
        /// </summary>
        private void SeedTroopOnAll(string characterId, int count)
        {
            SeedTroop(Server, characterId, count);
            foreach (var client in Clients)
            {
                SeedTroop(client, characterId, count);
            }
        }

        private void SeedTroop(EnvironmentInstance instance, string characterId, int count)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Assert.True(instance.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                    Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));

                    // Seed via AddNewElement + AddToCountsAtIndex, which AllowedThread suppresses.
                    // AddToCounts must not be used here: TroopRosterAddToCountsPatch intentionally
                    // publishes sync for AddToCounts even on an allowed thread (the recruitment flow
                    // runs under AllowedThread), so seeding through it would sync the seed too.
                    roster.AddNewElement(character, -1);
                    var index = roster.FindIndexOfTroop(character);
                    roster.AddToCountsAtIndex(index, count, woundedCountChange: 0, xpChange: 0, removeDepleted: false);
                }
            });
        }

        /// <summary>
        /// Adds a zero-count element for <paramref name="characterId"/> on every instance without
        /// triggering sync.
        /// </summary>
        private void SeedEmptyElementOnAll(string characterId)
        {
            SeedEmptyElement(Server, characterId);
            foreach (var client in Clients)
            {
                SeedEmptyElement(client, characterId);
            }
        }

        private void SeedEmptyElement(EnvironmentInstance instance, string characterId)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Assert.True(instance.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                    Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));

                    roster.AddNewElement(character, -1);
                }
            });
        }
        #endregion
    }
}
