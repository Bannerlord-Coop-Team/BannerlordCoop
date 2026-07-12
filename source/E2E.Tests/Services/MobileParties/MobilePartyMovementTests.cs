using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using Coop.Core.Server.Services.MobileParties.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using SandBox;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

public class MobilePartyMovementTests : SyncTestBase
{
    private readonly string MobilePartyId = "TestParty";
    private readonly string TargetPartyId = "TargetParty";
    private readonly string TargetSettlementId = "TargetSettlement";

    public MobilePartyMovementTests(ITestOutputHelper output) : base(output)
    {
        MobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        TargetPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        TargetSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        var clientNum = 1;

        foreach (var client in TestEnvironment.Clients)
        {
            client.Container.Resolve<IControllerIdProvider>().SetControllerId($"TestClient{clientNum++}");
        }
    }

    [Theory]
    [InlineData(AiBehavior.Hold)]
    [InlineData(AiBehavior.GoToPoint)]
    public void PartyAi_SetAiBehavior_EmitsAndReplaysVanillaPostCallState(AiBehavior behavior)
    {
        var startingMoveTarget = new CampaignVec2(new Vec2(0.65f, 0.75f), true);
        var startingBehaviorTarget = new CampaignVec2(new Vec2(0.25f, 0.35f), true);
        var requestedBehaviorTarget = new CampaignVec2(new Vec2(0.85f, 0.95f), true);
        var server = TestEnvironment.Server;
        BehaviorState authoritative = default;

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out var serverParty));
            Assert.False(serverParty.IsMainParty);

            PrepareSetAiBehaviorState(serverParty, startingMoveTarget, startingBehaviorTarget);
            InvokeSetAiBehavior(serverParty, behavior, requestedBehaviorTarget);
            FlushCoalescer(server);

            authoritative = new BehaviorState(serverParty);
            Assert.Equal(behavior, authoritative.ShortTermBehavior);
            Assert.Equal(MobileParty.NavigationType.Default, authoritative.DesiredAiNavigationType);
            Assert.Equal(
                behavior == AiBehavior.Hold ? startingMoveTarget : requestedBehaviorTarget,
                authoritative.MoveTargetPoint);
            Assert.Equal(
                behavior == AiBehavior.Hold ? MoveModeType.Hold : MoveModeType.Point,
                authoritative.PartyMoveMode);
            Assert.Equal(requestedBehaviorTarget, authoritative.BehaviorTarget);
        });

        var compactPartyId = ObjectManager.Compact(TargetPartyId, typeof(MobileParty));
        var sent = Assert.Single(
            server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>(),
            message => message.BehaviorUpdateData.MobilePartyId == compactPartyId);
        var emitted = sent.BehaviorUpdateData;
        Assert.Equal(authoritative.ShortTermBehavior, emitted.NewAiBehavior);
        Assert.Equal(authoritative.DesiredAiNavigationType, emitted.DesiredAiNavigationType);
        Assert.Equal(authoritative.MoveTargetPoint, emitted.MoveTargetPoint);
        Assert.Equal(authoritative.BehaviorTarget, emitted.BestTargetPoint);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out var clientParty));
            AssertBehaviorState(authoritative, clientParty);
        }
    }

    [Fact]
    public void ServerSetAiBehavior_ClientOwnedParty_DoesNotMutateOrBroadcast()
    {
        var server = TestEnvironment.Server;
        var startingMoveTarget = new CampaignVec2(new Vec2(0.2f, 0.3f), true);
        var startingBehaviorTarget = new CampaignVec2(new Vec2(0.4f, 0.5f), true);
        var requestedBehaviorTarget = new CampaignVec2(new Vec2(0.8f, 0.9f), true);
        var compactPartyId = ObjectManager.Compact(MobilePartyId, typeof(MobileParty));
        BehaviorState expected = default;

        server.Call(() =>
        {
            var playerManager = server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(
                "TestClient1",
                string.Empty,
                MobilePartyId,
                string.Empty,
                string.Empty)));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var party));

            PrepareSetAiBehaviorState(party, startingMoveTarget, startingBehaviorTarget);
            expected = new BehaviorState(party);

            Assert.False(party.IsControlledByThisInstance());
            Assert.Equal(AiBehavior.Hold, expected.ShortTermBehavior);
            Assert.Equal(MoveModeType.Hold, expected.PartyMoveMode);
            FlushCoalescer(server);
        });

        server.NetworkSentMessages.Clear();

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var party));
            Assert.False(party.IsControlledByThisInstance());

            InvokeSetAiBehavior(party, AiBehavior.GoToPoint, requestedBehaviorTarget);

            AssertBehaviorState(expected, party);
            FlushCoalescer(server);
        });

        Assert.DoesNotContain(
            server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>(),
            message => message.BehaviorUpdateData.MobilePartyId == compactPartyId);
    }

    [Fact]
    public void ClientSetAiBehavior_ReplacesStaleServerMovementStateAndConvergesObserver()
    {
        var owner = TestEnvironment.Clients.First();
        var observer = TestEnvironment.Clients.Skip(1).First();
        var server = TestEnvironment.Server;
        var desiredTargetPosition = new CampaignVec2(new Vec2(0.75f, 0.85f), true);
        var staleTargetPosition = new CampaignVec2(new Vec2(0.15f, 0.25f), true);

        owner.Call(() =>
        {
            var playerManager = owner.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(
                "TestClient1",
                string.Empty,
                MobilePartyId,
                string.Empty,
                string.Empty)));
        });

        server.Call(() =>
        {
            var playerManager = server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(
                "TestClient1",
                string.Empty,
                MobilePartyId,
                string.Empty,
                string.Empty)));
            playerManager.SetPeer("TestClient1", owner.NetPeer);
        });

        foreach (var instance in new[] { TestEnvironment.Server, observer })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out var targetParty));

                targetParty.Position = staleTargetPosition;
                using (new AllowedThread())
                {
                    party.SetMoveEngageParty(targetParty, MobileParty.NavigationType.None);
                }

                Assert.Equal(AiBehavior.EngageParty, party.DefaultBehavior);
                Assert.Same(targetParty, party.TargetParty);
            });
        }

        owner.Call(() =>
        {
            Assert.True(owner.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var party));
            Assert.True(owner.ObjectManager.TryGetObject<Settlement>(TargetSettlementId, out var targetSettlement));

            targetSettlement.GatePosition = desiredTargetPosition;
            using (new AllowedThread())
            {
                party.SetMoveGoToSettlement(
                    targetSettlement,
                    MobileParty.NavigationType.Default,
                    isTargetingThePort: false);
            }

            party.Ai.BehaviorTarget = staleTargetPosition;
            InvokeSetAiBehavior(
                party,
                AiBehavior.GoToSettlement,
                desiredTargetPosition,
                targetSettlement.Party);
        });

        Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));
        Assert.True(server.ObjectManager.TryGetObject<Settlement>(TargetSettlementId, out var serverTargetSettlement));
        Assert.Equal(AiBehavior.GoToSettlement, serverParty.DefaultBehavior);
        Assert.Equal(AiBehavior.GoToSettlement, serverParty.ShortTermBehavior);
        Assert.Null(serverParty.TargetParty);
        Assert.Same(serverTargetSettlement, serverParty.TargetSettlement);
        Assert.Equal(desiredTargetPosition, serverParty.TargetPosition);
        Assert.Equal(desiredTargetPosition, serverParty.MoveTargetPoint);
        Assert.Equal(desiredTargetPosition, serverParty.Ai.BehaviorTarget);
        Assert.Equal(MobileParty.NavigationType.Default, serverParty.DesiredAiNavigationType);
        Assert.Equal(MoveModeType.Point, serverParty.PartyMoveMode);

        server.Call(() => FlushCoalescer(server));

        Assert.True(observer.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var observerParty));
        AssertPartyMovementValues(observer, observerParty);
    }

    [Fact]
    public void FailedClientSnapshotReplay_DoesNotBroadcastPartiallyAppliedState()
    {
        var server = TestEnvironment.Server;
        var compactPartyId = ObjectManager.Compact(MobilePartyId, typeof(MobileParty));

        server.Call(() => FlushCoalescer(server));
        server.NetworkSentMessages.Clear();

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var party));

            var data = new PartyBehaviorUpdateData(
                compactPartyId,
                AiBehavior.EngageParty,
                null,
                party.Position,
                false,
                party.Position,
                AiBehavior.Hold,
                party.TargetPosition,
                MobileParty.NavigationType.Default)
            {
                OriginControllerId = "TestClient1",
                MoveTargetPoint = party.MoveTargetPoint,
                NextTargetPosition = party.NextTargetPosition,
                PartyMoveMode = MoveModeType.Hold,
            };

            server.Resolve<IMessageBroker>().Publish(this, new UpdatePartyBehavior(ref data));

            // Replay reaches UpdateBehavior after mutating the short-term behavior, where vanilla
            // rejects EngageParty without a mobile PartyBase. A failed replay must not be echoed.
            Assert.Equal(AiBehavior.EngageParty, party.ShortTermBehavior);
            FlushCoalescer(server);
        });

        Assert.DoesNotContain(
            server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>(),
            message => message.BehaviorUpdateData.MobilePartyId == compactPartyId);
    }

    [Fact]
    public void DestroyAttachedParty_DropsQueuedBehaviorBeforeDestroy_AndSendsNoLateUpdate()
    {
        var server = TestEnvironment.Server;
        var compactAttachedPartyId = ObjectManager.Compact(TargetPartyId, typeof(MobileParty));

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var leaderParty));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out var attachedParty));

            attachedParty.AttachedTo = leaderParty;
            Assert.Same(leaderParty, attachedParty.AttachedTo);
            FlushCoalescer(server);
        });
        server.NetworkSentMessages.Clear();

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var leaderParty));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out var attachedParty));
            Assert.Same(leaderParty, attachedParty.AttachedTo);

            attachedParty.SetMoveGoToPoint(
                new CampaignVec2(new Vec2(0.35f, 0.45f), true),
                MobileParty.NavigationType.Default);
            Assert.True(server.Resolve<ISendCoalescer>().HasPending);

            DestroyPartyAction.Apply(null, attachedParty);

            Assert.False(server.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out _));
            int destroyIndex = server.NetworkSentMessages.Messages.FindIndex(
                message => message is NetworkDestroyInstance<MobileParty> destroy &&
                    destroy.InstanceId == TargetPartyId);
            Assert.True(destroyIndex >= 0, "The MobileParty destroy must be sent during teardown.");

            FlushCoalescer(server);

            Assert.DoesNotContain(
                server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>(),
                message => message.BehaviorUpdateData.MobilePartyId == compactAttachedPartyId);
            Assert.DoesNotContain(
                server.NetworkSentMessages.Messages
                    .Skip(destroyIndex + 1)
                    .OfType<NetworkUpdatePartyBehavior>(),
                message => message.BehaviorUpdateData.MobilePartyId == compactAttachedPartyId);

            int messageCountAfterFlush = server.NetworkSentMessages.Count;
            FlushCoalescer(server);
            Assert.Equal(messageCountAfterFlush, server.NetworkSentMessages.Count);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out _));
        }
    }

    [Fact]
    public void Party_SetMoveHold_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));
            serverParty.SetMoveGoToPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.SetMoveModeHold();
            serverParty.Ai.Tick(dt);
            FlushCoalescer(server);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void Party_SetMoveEngageParty_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;


        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out var targetParty));
            serverParty.SetMoveEngageParty(targetParty, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
            FlushCoalescer(server);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void Party_SetMoveGoAroundParty_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;

        Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out var targetParty));
            serverParty.SetMoveGoAroundParty(targetParty, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
            FlushCoalescer(server);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void Party_SetMoveGoToSettlement_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;

        Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(TargetSettlementId, out var targetSettlement));
            serverParty.SetMoveGoToSettlement(targetSettlement, MobileParty.NavigationType.Default, false);
            serverParty.Ai.Tick(dt);
            FlushCoalescer(server);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void Party_SetTargetSettlementStandalone_SyncsCompleteMovementState()
    {
        var server = TestEnvironment.Server;
        var portPosition = new CampaignVec2(new Vec2(0.35f, 0.45f), false);

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(TargetSettlementId, out var targetSettlement));

            using (new AllowedThread())
            {
                targetSettlement.PortPosition = portPosition;
            }

            serverParty.SetTargetSettlement(targetSettlement, isTargetingPort: true);
            FlushCoalescer(server);

            Assert.Same(targetSettlement, serverParty.TargetSettlement);
            Assert.True(serverParty.IsTargetingPort);
            Assert.Equal(portPosition, serverParty.MoveTargetPoint);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void PartyAi_SetMoveGoToPoint_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;

        Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));

        // Act
        server.Call(() =>
        {
            
            serverParty.SetMoveGoToPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
            FlushCoalescer(server);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void Party_SetMoveToNearestLand_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;

        Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));
        Assert.True(server.ObjectManager.TryGetObject<Settlement>(TargetSettlementId, out var targetSettlement));

        // Act
        server.Call(() =>
        {
            serverParty.SetMoveToNearestLand(targetSettlement);
            serverParty.SetShortTermBehavior(AiBehavior.AssaultSettlement, targetSettlement.Party);
            serverParty.Ai.Tick(dt);
            FlushCoalescer(server);
        }, disabledMethods: new MethodBase[] { 
            AccessTools.Method(typeof(MapScene), nameof(MapScene.GetNearestFaceCenterForPositionWithPath))
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            client.Call(() =>
            {
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void Party_SetMoveGoToInteractablePoint_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;

        Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));


        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(TargetSettlementId, out var targetSettlement));
            serverParty.SetMoveGoToInteractablePoint(targetSettlement.Party, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
            FlushCoalescer(server);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            client.Call(() =>
            {
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void PartyAi_SetAiBehaviorWithAnchorPoint_SyncsInteractableOwner()
    {
        var server = TestEnvironment.Server;
        var startingTarget = new CampaignVec2(new Vec2(0.25f, 0.15f), true);
        var behaviorTarget = new CampaignVec2(new Vec2(0.45f, 0.35f), true);

        foreach (var instance in new[] { server }.Concat(TestEnvironment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out var party));

                using (new AllowedThread())
                {
                    party.TargetPosition = startingTarget;
                    party.MoveTargetPoint = startingTarget;
                    party.NextTargetPosition = startingTarget;
                    party.PartyMoveMode = MoveModeType.Hold;
                    party.MoveTargetParty = null;
                    party.Ai.BehaviorTarget = startingTarget;
                    party.Ai.AiBehaviorInteractable = null;
                }
            });
        }

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out var serverParty));

            InvokeSetAiBehavior(
                serverParty,
                AiBehavior.GoToPoint,
                behaviorTarget,
                serverParty.Anchor);
            FlushCoalescer(server);

            Assert.Same(serverParty.Anchor, serverParty.Ai.AiBehaviorInteractable);
            Assert.Equal(behaviorTarget, serverParty.Ai.BehaviorTarget);
        });

        var compactPartyId = ObjectManager.Compact(TargetPartyId, typeof(MobileParty));
        var sent = Assert.Single(
            server.NetworkSentMessages
                .GetMessages<NetworkUpdatePartyBehavior>()
                .Where(message => message.BehaviorUpdateData.MobilePartyId == compactPartyId));
        Assert.True(sent.BehaviorUpdateData.HasTarget);
        Assert.Equal(BehaviorInteractableKind.AnchorPoint, sent.BehaviorUpdateData.InteractableKind);
        Assert.Equal(compactPartyId, sent.BehaviorUpdateData.InteractablePointId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out var clientParty));
            AssertPartyMovementValues(client, clientParty, TargetPartyId);
        }
    }

    [Fact]
    public void Party_SetMoveEscortParty_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;


        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out var targetParty));
            serverParty.SetMoveEscortParty(targetParty, MobileParty.NavigationType.Default, false);
            serverParty.Ai.Tick(dt);
            FlushCoalescer(server);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            client.Call(() =>
            {
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void Party_SetMovePatrolAroundPoint_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;


        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));
            serverParty.SetMovePatrolAroundPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
            FlushCoalescer(server);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            client.Call(() =>
            {
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void Party_SetMovePatrolAroundSettlement_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;


        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(TargetSettlementId, out var targetSettlement));
            serverParty.SetMovePatrolAroundSettlement(targetSettlement, MobileParty.NavigationType.Default, false);
            serverParty.Ai.Tick(dt);
            FlushCoalescer(server);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            client.Call(() =>
            {
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void Party_SetMoveRaidSettlement_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;


        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(TargetSettlementId, out var targetSettlement));
            serverParty.SetMoveRaidSettlement(targetSettlement, MobileParty.NavigationType.Default, false);
            serverParty.Ai.Tick(dt);
            FlushCoalescer(server);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            client.Call(() =>
            {
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void Party_SetMoveBesiegeSettlement_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;


        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(TargetSettlementId, out var targetSettlement));
            serverParty.SetMoveBesiegeSettlement(targetSettlement, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
            FlushCoalescer(server);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            client.Call(() =>
            {
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void Party_SetMoveDefendSettlement_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;


        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(TargetSettlementId, out var targetSettlement));
            serverParty.SetMoveDefendSettlement(targetSettlement, false, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
            FlushCoalescer(server);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            client.Call(() =>
            {
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            AssertPartyMovementValues(client, clientParty);
        }
    }

    private static void PrepareSetAiBehaviorState(
        MobileParty party,
        CampaignVec2 moveTargetPoint,
        CampaignVec2 behaviorTarget)
    {
        using (new AllowedThread())
        {
            party.SetMoveModeHold();
        }

        party.DesiredAiNavigationType = MobileParty.NavigationType.None;
        party.MoveTargetPoint = moveTargetPoint;
        party.Ai.BehaviorTarget = behaviorTarget;
        party.Ai.AiBehaviorInteractable = null;
    }

    private static void InvokeSetAiBehavior(
        MobileParty party,
        AiBehavior behavior,
        CampaignVec2 behaviorTarget,
        IInteractablePoint interactablePoint = null)
    {
        party.Ai.SetAiBehavior(behavior, interactablePoint, behaviorTarget);
    }

    private static void AssertBehaviorState(BehaviorState expected, MobileParty actual)
    {
        Assert.Equal(expected.ShortTermBehavior, actual.ShortTermBehavior);
        Assert.Equal(expected.DesiredAiNavigationType, actual.DesiredAiNavigationType);
        Assert.Equal(expected.MoveTargetPoint, actual.MoveTargetPoint);
        Assert.Equal(expected.PartyMoveMode, actual.PartyMoveMode);
        Assert.Equal(expected.BehaviorTarget, actual.Ai.BehaviorTarget);
    }

    /// <summary>
    /// Captures the behavior fields compared after server/client replay.
    /// </summary>
    private readonly struct BehaviorState
    {
        public AiBehavior ShortTermBehavior { get; }
        public MobileParty.NavigationType DesiredAiNavigationType { get; }
        public CampaignVec2 MoveTargetPoint { get; }
        public MoveModeType PartyMoveMode { get; }
        public CampaignVec2 BehaviorTarget { get; }

        public BehaviorState(MobileParty party)
        {
            ShortTermBehavior = party.ShortTermBehavior;
            DesiredAiNavigationType = party.DesiredAiNavigationType;
            MoveTargetPoint = party.MoveTargetPoint;
            PartyMoveMode = party.PartyMoveMode;
            BehaviorTarget = party.Ai.BehaviorTarget;
        }
    }

    private void AssertPartyMovementValues(
        EnvironmentInstance client,
        MobileParty clientParty,
        string partyId = null)
    {
        var server = TestEnvironment.Server;
        Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId ?? MobilePartyId, out var serverParty));

        Assert.Equal(serverParty.DefaultBehavior, clientParty.DefaultBehavior);
        Assert.Equal(serverParty.ShortTermBehavior, clientParty.ShortTermBehavior);
        Assert.Equal(serverParty.TargetPosition, clientParty.TargetPosition);
        Assert.Equal(serverParty.MoveTargetPoint, clientParty.MoveTargetPoint);
        Assert.Equal(serverParty.NextTargetPosition, clientParty.NextTargetPosition);
        Assert.Equal(serverParty.DesiredAiNavigationType, clientParty.DesiredAiNavigationType);
        Assert.Equal(serverParty.IsTargetingPort, clientParty.IsTargetingPort);
        Assert.Equal(serverParty.PartyMoveMode, clientParty.PartyMoveMode);
        Assert.Equal(serverParty.Ai.BehaviorTarget, clientParty.Ai.BehaviorTarget);

        AssertSameReference(client, serverParty.TargetParty, clientParty.TargetParty);
        AssertSameReference(client, serverParty.TargetSettlement, clientParty.TargetSettlement);
        AssertSameReference(client, serverParty.MoveTargetParty, clientParty.MoveTargetParty);
        AssertSameReference(client, serverParty.Ai.AiBehaviorPartyBase, clientParty.Ai.AiBehaviorPartyBase);
        AssertInteractableReference(
            client,
            serverParty.Ai.AiBehaviorInteractable,
            clientParty.Ai.AiBehaviorInteractable);
    }

    private void AssertInteractableReference(
        EnvironmentInstance client,
        IInteractablePoint serverValue,
        IInteractablePoint clientValue)
    {
        if (serverValue == null)
        {
            Assert.Null(clientValue);
            return;
        }

        if (serverValue is PartyBase serverPartyBase)
        {
            AssertSameReference(client, serverPartyBase, Assert.IsType<PartyBase>(clientValue));
            return;
        }

        if (serverValue is AnchorPoint serverAnchor)
        {
            var clientAnchor = Assert.IsType<AnchorPoint>(clientValue);
            AssertSameReference(client, serverAnchor.Owner, clientAnchor.Owner);
            return;
        }

        Assert.Fail($"Unsupported interactable type {serverValue.GetType().FullName}");
    }

    private void AssertSameReference<T>(EnvironmentInstance client, T serverValue, T clientValue)
        where T : class
    {
        if (serverValue == null)
        {
            Assert.Null(clientValue);
            return;
        }

        Assert.NotNull(clientValue);
        Assert.True(TestEnvironment.Server.ObjectManager.TryGetId(serverValue, out var serverId));
        Assert.True(client.ObjectManager.TryGetId(clientValue, out var clientId));
        Assert.Equal(serverId, clientId);
    }

    private static void FlushCoalescer(EnvironmentInstance server)
    {
        server.Resolve<ISendCoalescer>().Flush(server.Resolve<INetwork>());
    }
}
