using System;
using Common.Messaging;
using Common.Network.Coalescing;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.TroopRosters
{
    /// <summary>
    /// End to end tests for the identity-keyed, ordered-batch TroopRoster sync handled by
    /// <see cref="GameInterface.Services.TroopRosters.Handlers.TroopRosterDeltaHandler"/>.
    /// </summary>
    /// <remarks>
    /// Each test drives an authoritative <see cref="TroopRoster"/> mutation on the server. The server
    /// patch publishes a local event carrying the server index; the handler resolves the element's
    /// identity from the server roster and batches its operations, which the client applies
    /// through the same vanilla mutator, found by character. The client roster starts empty, so the
    /// AddToCounts tests also prove a positive add creates the element (with correct cached totals) on an
    /// under-populated client; the Set tests seed the element first, since an absolute Set for a troop the
    /// client does not have is skipped rather than inventing an element without its earlier create delta.
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
            FlushCoalescer();

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
            FlushCoalescer();

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
            FlushCoalescer();

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
                Assert.Equal(12, roster.TotalManCount);
            });
            FlushCoalescer();

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out _, CharacterId1);
                Assert.Equal(12, roster.GetElementCopyAtIndex(0).Number);
                Assert.Equal(12, roster.TotalManCount);
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
                Assert.Equal(3, roster.TotalWounded);
                Assert.Equal(2, roster.TotalHealthyCount);
            });
            FlushCoalescer();

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out _, CharacterId1);
                Assert.Equal(3, roster.GetElementCopyAtIndex(0).WoundedNumber);
                Assert.Equal(3, roster.TotalWounded);
                Assert.Equal(2, roster.TotalHealthyCount);
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
            FlushCoalescer();

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out _, CharacterId1);
                Assert.Equal(250, roster.GetElementCopyAtIndex(0).Xp);
            }
        }

        [Fact]
        public void AiParty_XpMutationsAreSuppressedWhileCountsReachEveryClient()
        {
            var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
            string rosterId = null;

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.False(party.IsPlayerParty());
                Assert.True(Server.ObjectManager.TryGetId(party.MemberRoster, out rosterId));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));
                party.MemberRoster.AddToCounts(character, 5);
            });
            FlushCoalescer();
            Server.NetworkSentMessages.Clear();
            foreach (var client in Clients) client.InternalMessages.Clear();

            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character, rosterId, CharacterId1);
                roster.SetElementXp(roster.FindIndexOfTroop(character), 250);
            });
            FlushCoalescer();

            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkTroopRosterElementBatch>());
            foreach (var client in Clients)
            {
                Assert.Empty(client.InternalMessages.GetMessages<NetworkTroopRosterElementBatch>());
            }

            Server.NetworkSentMessages.Clear();
            foreach (var client in Clients) client.InternalMessages.Clear();
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character, rosterId, CharacterId1);
                roster.AddToCounts(character, 1, xpChange: 75);
            });
            FlushCoalescer();

            var addOperation = Assert.Single(Server.NetworkSentMessages
                .GetMessages<NetworkTroopRosterElementBatch>()
                .SelectMany(batch => batch.Operations));
            Assert.Equal(TroopRosterElementOperationKind.AddCounts, addOperation.Kind);
            Assert.Equal(0, addOperation.Xp);
            foreach (var client in Clients)
            {
                var received = Assert.Single(client.InternalMessages
                    .GetMessages<NetworkTroopRosterElementBatch>()
                    .SelectMany(batch => batch.Operations));
                Assert.Equal(TroopRosterElementOperationKind.AddCounts, received.Kind);
                Assert.Equal(1, received.Count);
                Assert.Equal(0, received.Xp);
            }
        }

        [Fact]
        public void ConnectedPlayerParty_ControllerReceivesXpAndObserversReceiveOnlyCounts()
        {
            var controller = Clients.First();
            var observer = Clients.Last();
            var (partyId, _) = CreatePlayerParty("controller", controller, connected: true);
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));
                party.MemberRoster.AddToCounts(character, 5);
            });
            FlushCoalescer();

            controller.InternalMessages.Clear();
            observer.InternalMessages.Clear();
            Server.NetworkSentMessages.Clear();
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));
                party.MemberRoster.SetElementXp(party.MemberRoster.FindIndexOfTroop(character), 100);
                party.MemberRoster.SetElementXp(party.MemberRoster.FindIndexOfTroop(character), 200);
                party.MemberRoster.SetElementXp(party.MemberRoster.FindIndexOfTroop(character), 250);
            });
            FlushCoalescer();

            var setXp = Assert.Single(controller.InternalMessages
                .GetMessages<NetworkTroopRosterElementBatch>()
                .SelectMany(batch => batch.Operations));
            Assert.Equal(TroopRosterElementOperationKind.SetXp, setXp.Kind);
            Assert.Equal(250, setXp.Xp);
            Assert.Empty(observer.InternalMessages.GetMessages<NetworkTroopRosterElementBatch>());

            controller.InternalMessages.Clear();
            observer.InternalMessages.Clear();
            Server.NetworkSentMessages.Clear();
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));
                party.MemberRoster.AddToCounts(character, 1, xpChange: 75);
                party.MemberRoster.SetElementXp(party.MemberRoster.FindIndexOfTroop(character), 400);
                party.MemberRoster.AddToCounts(character, 2, xpChange: 25);
            });
            FlushCoalescer();

            var controllerOperations = controller.InternalMessages
                .GetMessages<NetworkTroopRosterElementBatch>()
                .SelectMany(batch => batch.Operations)
                .ToArray();
            Assert.Collection(controllerOperations,
                firstAdd =>
                {
                    Assert.Equal(TroopRosterElementOperationKind.AddCounts, firstAdd.Kind);
                    Assert.Equal(1, firstAdd.Count);
                    Assert.Equal(75, firstAdd.Xp);
                },
                absoluteSet =>
                {
                    Assert.Equal(TroopRosterElementOperationKind.SetXp, absoluteSet.Kind);
                    Assert.Equal(400, absoluteSet.Xp);
                },
                secondAdd =>
                {
                    Assert.Equal(TroopRosterElementOperationKind.AddCounts, secondAdd.Kind);
                    Assert.Equal(2, secondAdd.Count);
                    Assert.Equal(25, secondAdd.Xp);
                });

            var observerOperations = observer.InternalMessages
                .GetMessages<NetworkTroopRosterElementBatch>()
                .SelectMany(batch => batch.Operations)
                .ToArray();
            Assert.Collection(observerOperations,
                firstAdd =>
                {
                    Assert.Equal(TroopRosterElementOperationKind.AddCounts, firstAdd.Kind);
                    Assert.Equal(1, firstAdd.Count);
                    Assert.Equal(0, firstAdd.Xp);
                },
                secondAdd =>
                {
                    Assert.Equal(TroopRosterElementOperationKind.AddCounts, secondAdd.Kind);
                    Assert.Equal(2, secondAdd.Count);
                    Assert.Equal(0, secondAdd.Xp);
                });

            Assert.Equal(2, Server.NetworkSentMessages
                .GetMessages<NetworkTroopRosterElementBatch>()
                .Count());
        }

        [Fact]
        public void DisconnectedPlayerParty_ObserversReceiveCountsWithoutXp()
        {
            var (partyId, _) = CreatePlayerParty("disconnected", Clients.First(), connected: false);

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));
                party.MemberRoster.AddToCounts(character, 5);
            });
            FlushCoalescer();

            Server.NetworkSentMessages.Clear();
            foreach (var client in Clients) client.InternalMessages.Clear();
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));
                party.MemberRoster.AddToCounts(character, 1, xpChange: 100);
            });
            FlushCoalescer();

            var operation = Assert.Single(Server.NetworkSentMessages
                .GetMessages<NetworkTroopRosterElementBatch>()
                .SelectMany(batch => batch.Operations));
            Assert.Equal(TroopRosterElementOperationKind.AddCounts, operation.Kind);
            Assert.Equal(0, operation.Xp);

            foreach (var client in Clients)
            {
                var received = Assert.Single(client.InternalMessages
                    .GetMessages<NetworkTroopRosterElementBatch>()
                    .SelectMany(batch => batch.Operations));
                Assert.Equal(TroopRosterElementOperationKind.AddCounts, received.Kind);
                Assert.Equal(1, received.Count);
                Assert.Equal(0, received.Xp);
            }


            Server.NetworkSentMessages.Clear();
            foreach (var client in Clients) client.InternalMessages.Clear();
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(CharacterId1, out var character));
                party.MemberRoster.SetElementXp(party.MemberRoster.FindIndexOfTroop(character), 250);
            });
            FlushCoalescer();

            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkTroopRosterElementBatch>());
            foreach (var client in Clients)
            {
                Assert.Empty(client.InternalMessages.GetMessages<NetworkTroopRosterElementBatch>());
            }
        }

        [Fact]
        public void Server_AddCountsAndSetXp_SendsOneBatch()
        {
            Server.NetworkSentMessages.Clear();

            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character, CharacterId1);
                roster.AddToCounts(character, 5);
                roster.SetElementXp(roster.FindIndexOfTroop(character), 250);
            });
            FlushCoalescer();

            Assert.Single(Server.NetworkSentMessages);
            var batch = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkTroopRosterElementBatch>());
            Assert.Collection(batch.Operations,
                addCounts =>
                {
                    Assert.Equal(TroopRosterElementOperationKind.AddCounts, addCounts.Kind);
                    Assert.Equal(5, addCounts.Count);
                },
                setXp =>
                {
                    Assert.Equal(TroopRosterElementOperationKind.SetXp, setXp.Kind);
                    Assert.Equal(250, setXp.Xp);
                });
        }

        [Fact]
        public void Server_AdjacentAddCounts_ReplayNonCommutativeWoundedClampInOrder()
        {
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character, CharacterId1);
                roster.AddToCounts(character, 5, woundedCount: 5, xpChange: 100);
                roster.AddToCounts(character, -4, xpChange: 50);
                roster.AddToCounts(character, 4, xpChange: 7);

                Assert.True(Server.Resolve<ISendCoalescer>().HasPending);
            });

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out _, CharacterId1);
                Assert.Equal(0, roster.Count);
            }

            FlushCoalescer();

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out _, CharacterId1);
                Assert.Equal(1, roster.Count);
                var element = roster.GetElementCopyAtIndex(0);
                Assert.Equal(5, element.Number);
                Assert.Equal(1, element.WoundedNumber);
                Assert.Equal(157, element.Xp);
                Assert.Equal(5, roster.TotalManCount);
            }
        }

        [Fact]
        public void Server_RemoveAndRecreateInOneBatch_DiscardsRemovedElementsXp()
        {
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character, CharacterId1);
                roster.AddToCounts(character, 1, xpChange: 100);
                roster.AddToCounts(character, -1, xpChange: 50, removeDepleted: true);
                roster.AddToCounts(character, 1, xpChange: 7);
            });
            FlushCoalescer();

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out _, CharacterId1);
                Assert.Equal(1, roster.Count);
                var element = roster.GetElementCopyAtIndex(0);
                Assert.Equal(1, element.Number);
                Assert.Equal(0, element.WoundedNumber);
                Assert.Equal(7, element.Xp);
            }
        }

        [Fact]
        public void Server_RemoveToZeroWithRemoveDepletedFalse_KeepsZeroCountElement()
        {
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character, CharacterId1);
                roster.AddToCounts(character, 2, xpChange: 10);
                roster.AddToCounts(character, -2, xpChange: 5, removeDepleted: false);
            });
            FlushCoalescer();

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out var character, CharacterId1);
                Assert.Equal(1, roster.Count);
                Assert.True(roster.Contains(character));
                var element = roster.GetElementCopyAtIndex(0);
                Assert.Equal(0, element.Number);
                Assert.Equal(0, element.WoundedNumber);
                Assert.Equal(15, element.Xp);
                Assert.Equal(0, roster.TotalManCount);
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
            FlushCoalescer();

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

                Assert.Equal(1, roster.Count);
                Assert.Same(character2, roster.GetElementCopyAtIndex(0).Character);
                Assert.Equal(4, roster.TotalManCount);
            });
            FlushCoalescer();

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out var character2, CharacterId2);
                Assert.Equal(1, roster.Count);
                Assert.Same(character2, roster.GetElementCopyAtIndex(0).Character);
                Assert.Equal(4, roster.TotalManCount);
            }
        }

        [Fact]
        public void Server_RemoveZeroCountHero_RecalculatesTotalsOnAllInstances()
        {
            string heroId = TestEnvironment.CreateRegisteredObject<Hero>();

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

                roster.AddToCounts(hero.CharacterObject, 1);
                roster.SetElementNumber(roster.FindIndexOfTroop(hero.CharacterObject), 0);

                Assert.Equal(1, roster.Count);
                Assert.Equal(1, roster.TotalManCount);

                roster.RemoveZeroCounts();

                Assert.Equal(0, roster.Count);
                Assert.Equal(0, roster.TotalManCount);
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var roster));
                Assert.Equal(0, roster.Count);
                Assert.Equal(0, roster.TotalManCount);
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
            FlushCoalescer();

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
            FlushCoalescer();

            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var character, CharacterId1);
                using (new AllowedThread())
                {
                    roster.AddToCounts(character, -5);
                }
            });
            FlushCoalescer();

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
            FlushCoalescer();

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
                broker.Publish(this, new NetworkTroopRosterElementBatch(TroopRosterId, CharacterId1,
                    new[] { TroopRosterElementOperation.AddCounts(-5, 0, 0, false) }));
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
            => Resolve(instance, out roster, out character, TroopRosterId, characterId);

        private static void Resolve(EnvironmentInstance instance, out TroopRoster roster,
            out CharacterObject character, string rosterId, string characterId)
        {
            Assert.True(instance.ObjectManager.TryGetObject<TroopRoster>(rosterId, out roster));
            Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(characterId, out character));
        }

        private void FlushCoalescer() => TestEnvironment.FlushCoalescer();

        private (string PartyId, string RosterId) CreatePlayerParty(
            string controllerId,
            EnvironmentInstance controller,
            bool connected)
        {
            string partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
            string rosterId = null;
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(Server.ObjectManager.TryGetId(party.MemberRoster, out rosterId));
                Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(
                    new Player(controllerId, null, partyId, null, null)));
            });

            if (connected)
            {
                TestEnvironment.ConnectRegisteredPlayer(controller, controllerId);
            }

            Server.NetworkSentMessages.Clear();
            return (partyId, rosterId);
        }
    }
}
