using Common.Messaging;
using Common.Util;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit.Abstractions;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace E2E.Tests.Services.Rafts;

/// <summary>
/// The server's one-shot move to shore for a player's raft against the movement reports its owner sends.
/// </summary>
public class RaftMoveToShoreTests : MapEventTestBase
{
    private const string ControllerId = "rafted-owner";

    private static readonly CampaignVec2 RaftPosition = new CampaignVec2(new Vec2(20f, 20f), false);
    private static readonly CampaignVec2 Shore = new CampaignVec2(new Vec2(25f, 22f), true);

    /// <summary>Passes the test output to the environment.</summary>
    public RaftMoveToShoreTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// A hold the owner sent while setting sail reached the server after the raft's order, so the raft never moved. The
    /// server keeps the order and every machine ends up heading for the shore.
    /// </summary>
    [Fact]
    public void OwnerHoldSentBeforeTheRaft_KeepsTheMoveToShoreEverywhere()
    {
        var (_, partyId) = CreatePlayerHeroParty(ControllerId);
        // An owning client takes the self-echo path, so a resend carrying its origin id would keep its stale hold
        Clients.First().Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            party.IsActive = true;
            party._isCurrentlyAtSea = true;
            party._position = RaftPosition;

            // What RaftStateChangeAction.ActivateRaftStateForParty leaves, without the navigation mesh MovePartyToTheClosestLand needs
            party.IsInRaftState = true;
            party.SetMoveGoToPoint(Shore, MobileParty.NavigationType.All);
        });
        TestEnvironment.FlushCoalescer();

        foreach (var client in Clients)
        {
            client.PumpGameThread();
            client.Call(() =>
            {
                // The owner's leave from the port holds its party locally after it already got the raft's order
                using (new AllowedThread())
                    client.GetRegisteredObject<MobileParty>(partyId).SetMoveModeHold();
            });
        }

        SendOwnerHold(partyId);
        TestEnvironment.FlushCoalescer();

        foreach (var instance in Clients.Append(Server))
        {
            instance.PumpGameThread();
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.Equal(AiBehavior.GoToPoint, party.DefaultBehavior);
                Assert.Equal(Shore, party.TargetPosition);
            });
        }
    }

    /// <summary>The same publish the server's behavior request packet handler makes for the owner's hold.</summary>
    private void SendOwnerHold(string partyId)
    {
        Server.Call(() =>
        {
            var hold = new PartyBehaviorUpdateData(
                mobilePartyId: Compact(partyId, typeof(MobileParty)),
                newAiBehavior: AiBehavior.Hold,
                interactablePointId: null,
                bestTargetPoint: RaftPosition,
                partyPosition: RaftPosition,
                defaultBehavior: AiBehavior.Hold,
                targetPosition: RaftPosition,
                desiredAiNavigationType: MobileParty.NavigationType.None)
            {
                OriginControllerId = ControllerId,
                PartyMoveMode = MoveModeType.Hold,
                MoveTargetPoint = RaftPosition,
                IsCurrentlyAtSea = true,
            };
            Server.Resolve<IMessageBroker>().Publish(this, new UpdatePartyBehavior(ref hold, alreadyOnGameThread: true));
        });
    }
}
