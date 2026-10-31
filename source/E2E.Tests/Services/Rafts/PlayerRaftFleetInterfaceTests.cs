using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.CoopSessionData;
using GameInterface.Services.NavalDLC;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Rafts.Interfaces;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Rafts;

/// <summary>
/// The server ending a player's raft at sea: the raft state ends everywhere and the cached fleet comes back without the
/// coast anchor cached at the stranding.
/// </summary>
public class PlayerRaftFleetInterfaceTests : SyncTestBase
{
    // Passes the test output to the environment.
    public PlayerRaftFleetInterfaceTests(ITestOutputHelper output) : base(output)
    {
    }

    // The naval scenario stager takes a rafted player off its raft at sea before the battle (R41b P4).
    [Fact]
    public void LeaveRaftAtSea_EndsTheRaftAndTakesTheCachedFleetUnanchored()
    {
        var (heroId, partyId) = CreateRaftedPlayer();

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            Server.Resolve<IPlayerRaftFleetInterface>().LeaveRaftAtSea(party);

            Assert.False(party.IsInRaftState);
            Assert.False(CachedFleets().ContainsKey(heroId));
            Assert.False(party.Anchor.IsValid);
            Assert.Equal(new Vec2(11f, 21f), party.Anchor.GetLastUsedDisembarkPosition().ToVec2());
        });
        foreach (var client in Clients)
        {
            client.PumpGameThread();
            client.Call(() => Assert.False(client.GetRegisteredObject<MobileParty>(partyId).IsInRaftState));
        }
    }

    // Only a rafted party has a raft to leave, so another party keeps the fleet cached for its player.
    [Fact]
    public void LeaveRaftAtSea_LeavesAPartyThatIsNotOnARaftAlone()
    {
        var (heroId, partyId) = CreateRaftedPlayer();

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            party.IsInRaftState = false;
            Server.Resolve<IPlayerRaftFleetInterface>().LeaveRaftAtSea(party);

            Assert.True(CachedFleets().ContainsKey(heroId));
        });
    }

    // A player released from captivity at sea is put on a raft, and its fleet is kept by value until it washes ashore.
    [Fact]
    public void StrandAfterRelease_CachesTheFleetAndPutsThePartyOnARaft()
    {
        var fleet = CreatePlayerFleetAtSea();

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(fleet.PartyId);
            Server.Resolve<IPlayerRaftFleetInterface>().StrandAfterRelease(party);

            Assert.True(party.IsInRaftState);
            Assert.Empty(party.Ships);

            var cached = CachedFleets()[fleet.HeroId];
            Assert.Equal(new Vec2(30f, 40f), new Vec2(cached.AnchorX, cached.AnchorY));
            Assert.False(cached.AnchorIsOnLand);
            Assert.Equal(new Vec2(31f, 41f), new Vec2(cached.DisembarkX, cached.DisembarkY));
            Assert.True(cached.DisembarkIsOnLand);

            // Template objects are cached by their StringId, which survives a reload
            var pieceStringId = Server.GetRegisteredObject<ShipUpgradePiece>(fleet.PieceId).StringId;
            var ship = Assert.Single(cached.Ships);
            Assert.Equal(Server.GetRegisteredObject<ShipHull>(fleet.HullId).StringId, ship.HullId);
            Assert.Equal("Sea Wolf", ship.Name);
            Assert.Equal(40f, ship.HitPoints);
            Assert.Equal(30f, ship.SailHitPoints);
            Assert.Equal(Server.GetRegisteredObject<Figurehead>(fleet.FigureheadId).StringId, ship.FigureheadId);
            Assert.Equal("sail_pattern_1", ship.CustomSailPatternId);
            Assert.Equal(7, ship.RandomValue);
            Assert.Equal(pieceStringId, ship.SlotPieceIds["slot1"]);
            Assert.Equal(new[] { pieceStringId }, ship.UnlockedPieceIds);
        }, RaftDisabledMethods);

        foreach (var client in Clients)
        {
            client.PumpGameThread();
            client.Call(() => Assert.True(client.GetRegisteredObject<MobileParty>(fleet.PartyId).IsInRaftState));
            Assert.False(client.ObjectManager.TryGetObject<Ship>(fleet.ShipId, out _));
        }
    }

    // Vanilla only strands a party that is still at sea and not already rafted.
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void StrandAfterRelease_LeavesAPartyOnLandOrAlreadyRaftedAlone(bool atSea, bool inRaftState)
    {
        var fleet = CreatePlayerFleetAtSea();

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(fleet.PartyId);
            party._isCurrentlyAtSea = atSea;
            party.IsInRaftState = inRaftState;
            Server.Resolve<IPlayerRaftFleetInterface>().StrandAfterRelease(party);

            Assert.Equal(inRaftState, party.IsInRaftState);
            Assert.Single(party.Ships);
            Assert.False(CachedFleets().ContainsKey(fleet.HeroId));
        }, RaftDisabledMethods);
    }

    // A party with no ships still ends up on a raft, with nothing cached to give back.
    [Fact]
    public void StrandAfterRelease_WithoutShips_RaftsWithoutCachingAFleet()
    {
        var fleet = CreatePlayerFleetAtSea();

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(fleet.PartyId);
            DestroyShipAction.Apply(party.Ships[0]);
            Server.Resolve<IPlayerRaftFleetInterface>().StrandAfterRelease(party);

            Assert.True(party.IsInRaftState);
            Assert.False(CachedFleets().ContainsKey(fleet.HeroId));
        }, RaftDisabledMethods);
    }

    // Washing ashore rebuilds each cached ship as a new registered ship and restores the anchor cached at the stranding.
    [Fact]
    public void ReturnFleet_RebuildsTheCachedShipsAndAnchor()
    {
        var fleet = CreatePlayerFleetAtSea();
        string returnedShipId = null;

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(fleet.PartyId);
            var raftFleetInterface = Server.Resolve<IPlayerRaftFleetInterface>();
            raftFleetInterface.StrandAfterRelease(party);
            party.Anchor.ResetPosition();
            party.Anchor.SetLastUsedDisembarkPosition(CampaignVec2.Invalid);

            raftFleetInterface.ReturnFleet(party);

            Assert.False(CachedFleets().ContainsKey(fleet.HeroId));
            var ship = Assert.Single(party.Ships);
            Assert.True(Server.ObjectManager.TryGetId(ship, out returnedShipId));
            Assert.Same(Server.GetRegisteredObject<ShipHull>(fleet.HullId), ship.ShipHull);
            Assert.Equal("Sea Wolf", ship.Name.ToString());
            Assert.Equal(40f, ship.HitPoints);
            Assert.Equal(30f, ship.SailHitPoints);
            Assert.Same(Server.GetRegisteredObject<Figurehead>(fleet.FigureheadId), ship.Figurehead);
            Assert.Equal("sail_pattern_1", ship.CustomSailPatternId);
            Assert.Equal(7, ship.RandomValue);
            Assert.Same(Server.GetRegisteredObject<ShipUpgradePiece>(fleet.PieceId), ship.GetPieceAtSlot("slot1"));

            Assert.Equal(new Vec2(30f, 40f), party.Anchor.Position.ToVec2());
            Assert.Equal(new Vec2(31f, 41f), party.Anchor.GetLastUsedDisembarkPosition().ToVec2());
        }, RaftDisabledMethods);

        foreach (var client in Clients)
        {
            client.PumpGameThread();
            var ship = client.GetRegisteredObject<Ship>(returnedShipId);
            Assert.Same(client.GetRegisteredObject<MobileParty>(fleet.PartyId).Party, ship.Owner);
            Assert.Same(client.GetRegisteredObject<ShipHull>(fleet.HullId), ship.ShipHull);
        }
    }

    // A cached ship whose hull no longer resolves is dropped instead of failing the whole fleet.
    [Fact]
    public void ReturnFleet_SkipsAShipWithAnUnknownHull()
    {
        var (heroId, partyId) = CreateRaftedPlayer();

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            var unknownHullShip = new CachedShipData("missing_hull", null, 10f, 10f, null, null, 0, null, null);
            CachedFleets()[heroId] = new PlayerCachedFleetData(new List<CachedShipData> { unknownHullShip }, 10f, 20f, false, 11f, 21f, true);

            Server.Resolve<IPlayerRaftFleetInterface>().ReturnFleet(party);

            Assert.False(CachedFleets().ContainsKey(heroId));
            Assert.Empty(party.Ships);
        });
    }

    // Raft state change moves the party to the closest land, which needs the navigation mesh.
    private static readonly MethodBase[] RaftDisabledMethods =
    {
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.MovePartyToTheClosestLand)),
    };

    private record PlayerFleet(string HeroId, string PartyId, string ShipId, string HullId, string PieceId, string FigureheadId);

    // A registered player's party at sea with one named, damaged ship that has a fitted piece and a figurehead.
    private PlayerFleet CreatePlayerFleetAtSea()
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var hullId = TestEnvironment.CreateRegisteredObject<ShipHull>();
        var slotId = TestEnvironment.CreateRegisteredObject<ShipSlot>();
        var pieceId = TestEnvironment.CreateRegisteredObject<ShipUpgradePiece>();
        var figureheadId = TestEnvironment.CreateRegisteredObject<Figurehead>();

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() => ConfigureHull(instance, hullId, slotId, pieceId));
        }

        string heroId = null;
        string shipId = null;
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out heroId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero.Clan, out var clanId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero.CharacterObject, out var characterId));
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("stranded", heroId, partyId, clanId, characterId)));

            var piece = Server.GetRegisteredObject<ShipUpgradePiece>(pieceId);
            var ship = new Ship(Server.GetRegisteredObject<ShipHull>(hullId));
            ChangeShipOwnerAction.ApplyByProduction(party.Party, ship);
            ship.SetName(new TextObject("Sea Wolf"));
            ship.ChangeFigurehead(Server.GetRegisteredObject<Figurehead>(figureheadId));
            ship.CustomSailPatternId = "sail_pattern_1";
            ship.RandomValue = 7;
            ship._unlockedUpgradePieces.Add(piece);
            ship.EquipUpgradePiece("slot1", piece);
            ship.HitPoints = 40f;
            ship.SailHitPoints = 30f;
            Assert.True(Server.ObjectManager.TryGetId(ship, out shipId));

            party._isCurrentlyAtSea = true;
            party.Anchor.SetPosition(new CampaignVec2(new Vec2(30f, 40f), false));
            party.Anchor.SetLastUsedDisembarkPosition(new CampaignVec2(new Vec2(31f, 41f), true));
        });

        return new PlayerFleet(heroId, partyId, shipId, hullId, pieceId, figureheadId);
    }

    // Hull fields are template data every machine loads the same, so each instance gets the same values.
    private static void ConfigureHull(EnvironmentInstance instance, string hullId, string slotId, string pieceId)
    {
        var hull = instance.GetRegisteredObject<ShipHull>(hullId);
        var slot = instance.GetRegisteredObject<ShipSlot>(slotId);
        hull.MaxHitPoints = 100;
        hull.MaxSailHitPoints = 100;
        hull.CanEquipFigurehead = true;
        hull.AvailableSlots = new Dictionary<string, ShipSlot> { { "slot1", slot } }.GetReadOnlyDictionary();
        instance.GetRegisteredObject<ShipUpgradePiece>(pieceId)._targetSlots = new MBList<ShipSlot> { slot };
    }

    // A registered player's party on a raft, with an empty fleet cached at a stranding whose anchor was on the coast.
    private (string heroId, string partyId) CreateRaftedPlayer()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("rafted", heroId, partyId, "MyClanId", "MyCharacterObjectId")));
            Server.GetRegisteredObject<MobileParty>(partyId).IsInRaftState = true;
            CachedFleets()[heroId] = new PlayerCachedFleetData(new List<CachedShipData>(), 10f, 20f, true, 11f, 21f, true);
        });
        return (heroId, partyId);
    }

    // The server session's fleets cached per player hero id.
    private Dictionary<string, PlayerCachedFleetData> CachedFleets() =>
        Server.Resolve<ICoopSessionProvider>().CoopSession.NavalPlayerData.PlayerCachedFleets;
}
