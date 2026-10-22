using E2E.Tests.Util;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Ships;
using GameInterface.Services.Ships.Messages;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Ships;

/// <summary>
/// M6 D8 (option A): a player's loot ships leave the losing party when the result is applied, so the party's finalize no
/// longer destroys them, and they stay addressable until the loot screen takes or discards them, or the hold ends.
/// </summary>
public class LootShipHoldTests : SyncTestBase
{
    public LootShipHoldTests(ITestOutputHelper output) : base(output)
    {
    }

    // R41a finding 5: the losing party was destroyed with its ships about 90 s before the loot screen closed.
    [Fact]
    public void HeldLootShips_SurviveTheLosingPartysRemoval_AndTheLootScreenStillTakesAndDiscardsThem()
    {
        var loserId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var looterId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var takenId = GiveShip(loserId);
        var discardedId = GiveShip(loserId);
        var unlistedId = GiveShip(loserId);
        Server.Call(() =>
        {
            var looter = Server.GetRegisteredObject<MobileParty>(looterId).Party;
            foreach (var shipId in new[] { takenId, discardedId, unlistedId })
                Hold().Hold(Server.GetRegisteredObject<Ship>(shipId), looter);
        });
        foreach (var client in Clients)
            client.Call(() =>
            {
                Assert.Null(client.GetRegisteredObject<Ship>(takenId).Owner);
                Assert.Empty(client.GetRegisteredObject<MobileParty>(loserId).Ships);
            });

        Server.Call(() => Server.GetRegisteredObject<MobileParty>(loserId).RemoveParty());
        AssertAlive(takenId);
        AssertAlive(discardedId);
        AssertAlive(unlistedId);

        // The loot screen's confirm takes one ship; closing it discards every offered ship the player left, and the hold
        // destroys a held ship the discard didn't list.
        Server.Call(() => ChangeShipOwnerAction.ApplyByLooting(Server.GetRegisteredObject<MobileParty>(looterId).Party,
            Server.GetRegisteredObject<Ship>(takenId)));
        DiscardLoot(looterId, takenId, discardedId);

        AssertDestroyed(discardedId);
        AssertDestroyed(unlistedId);
        AssertAlive(takenId);
        foreach (var instance in new[] { Server }.Concat(Clients))
            instance.Call(() => Assert.Same(instance.GetRegisteredObject<MobileParty>(looterId).Party,
                instance.GetRegisteredObject<Ship>(takenId).Owner));
    }

    // The ship screen opens only after the troop and item loot and any conversations, so a slow looter keeps its ships; only
    // its disconnect or the long fallback ends the hold.
    [Fact]
    public void UnclaimedLootShip_OutlivesASlowLootScreen_AndIsDestroyedWhenItsLooterDisconnectsOrTheFallbackRunsOut()
    {
        var loserId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var droppedLooterId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var looterId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var leftByTheDroppedId = GiveShip(loserId);
        var slowId = GiveShip(loserId);
        var heroId = TestEnvironment.CreateRegisteredObject<TaleWorlds.CampaignSystem.Hero>();
        var start = DateTime.UtcNow;
        Server.Call(() =>
        {
            ((LootShipHold)Hold()).UtcNow = () => start;
            // A player with no live peer: its owner is disconnected.
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("dropped", heroId, droppedLooterId, "MyClanId", "MyCharacterObjectId")));
            Hold().Hold(Server.GetRegisteredObject<Ship>(leftByTheDroppedId), Server.GetRegisteredObject<MobileParty>(droppedLooterId).Party);
            Hold().Hold(Server.GetRegisteredObject<Ship>(slowId), Server.GetRegisteredObject<MobileParty>(looterId).Party);
            ((LootShipHold)Hold()).UtcNow = () => start + TimeSpan.FromMinutes(30);
            Hold().Sweep();
        });

        AssertDestroyed(leftByTheDroppedId);
        AssertAlive(slowId);

        Server.Call(() =>
        {
            ((LootShipHold)Hold()).UtcNow = () => start + ((LootShipHold)Hold()).FallbackHoldTime + TimeSpan.FromSeconds(1);
            Hold().Sweep();
        });
        AssertDestroyed(slowId);
    }
    private ILootShipHold Hold() => Server.Resolve<ILootShipHold>();

    // A registered ship the server gives to the party through the vanilla production ownership path.
    private string GiveShip(string partyId)
    {
        var shipId = TestEnvironment.CreateRegisteredObject<Ship>();
        Server.Call(() => ChangeShipOwnerAction.ApplyByProduction(Server.GetRegisteredObject<MobileParty>(partyId).Party,
            Server.GetRegisteredObject<Ship>(shipId)));
        return shipId;
    }

    // The client's OnPlayerLootShipEnd discard of every ship it was offered, as LootShipsHandler sends it.
    private void DiscardLoot(string looterId, params string[] offeredShipIds)
    {
        uint partyHandle = 0;
        var shipHandles = new uint[offeredShipIds.Length];
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetHandle(Server.GetRegisteredObject<MobileParty>(looterId), out partyHandle));
            for (int i = 0; i < offeredShipIds.Length; i++)
                Assert.True(Server.ObjectManager.TryGetHandle(Server.GetRegisteredObject<Ship>(offeredShipIds[i]), out shipHandles[i]));
            Server.SimulateMessage(Clients.First().NetPeer, new NetworkDiscardLootShips(partyHandle, shipHandles));
        });
    }

    private void AssertAlive(string shipId)
    {
        foreach (var instance in new[] { Server }.Concat(Clients))
        {
            instance.PumpGameThread();
            Assert.True(instance.ObjectManager.TryGetObject<Ship>(shipId, out _), $"ship {shipId} was destroyed on an instance");
        }
    }

    private void AssertDestroyed(string shipId)
    {
        foreach (var instance in new[] { Server }.Concat(Clients))
        {
            instance.PumpGameThread();
            Assert.False(instance.ObjectManager.TryGetObject<Ship>(shipId, out _), $"ship {shipId} is still alive on an instance");
        }
    }
}
