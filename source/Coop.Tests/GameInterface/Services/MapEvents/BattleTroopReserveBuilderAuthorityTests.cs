using GameInterface.Services.MapEvents.TroopSupply;
using Xunit;

namespace Coop.Tests.GameInterface.Services.MapEvents;

/// <summary>
/// Unit tests for <see cref="BattleTroopReserveBuilder"/>'s deployment-authority precedence (#3 "army leader
/// deploys the army", increment 1). A party's own player fields it; an AI party in a player-led army falls to
/// that army leader; otherwise no player does (the host fields it). Player members of another player's army keep
/// their own party in this increment. The rules are pure static methods on the builder so they test with plain
/// controller-id strings, with no game objects.
/// </summary>
public class BattleTroopReserveBuilderAuthorityTests
{
    // --- ResolveOwningController: which controller owns the party's reserve (null => host) ---

    [Fact]
    public void SoloPlayerParty_FieldedByOwnPlayer()
    {
        Assert.Equal("playerB", BattleTroopReserveBuilder.ResolveOwningController(partyOwnerController: "playerB", armyLeaderController: null));
    }

    [Fact]
    public void LeaderOwnParty_FieldedBySelf()
    {
        // The leader's own party: it is both the owner and the army leader.
        Assert.Equal("playerA", BattleTroopReserveBuilder.ResolveOwningController(partyOwnerController: "playerA", armyLeaderController: "playerA"));
    }

    [Fact]
    public void PlayerMemberOfAnotherPlayersArmy_KeepsOwnParty()
    {
        // Increment 1: a player member keeps their own party (the leader fielding teammates' parties is increment 2).
        Assert.Equal("playerB", BattleTroopReserveBuilder.ResolveOwningController(partyOwnerController: "playerB", armyLeaderController: "playerA"));
    }

    [Fact]
    public void AiPartyInPlayerLedArmy_FieldedByLeader()
    {
        // The core #3 case: an AI lord in a player's army is fielded + deployed by that leader, not the host.
        Assert.Equal("playerA", BattleTroopReserveBuilder.ResolveOwningController(partyOwnerController: null, armyLeaderController: "playerA"));
    }

    [Fact]
    public void AiPartyWithNoPlayerArmy_FieldedByHost()
    {
        // No owning player and no player-led army -> null, i.e. the host fields it (enemy, or independent allied AI).
        Assert.Null(BattleTroopReserveBuilder.ResolveOwningController(partyOwnerController: null, armyLeaderController: null));
    }

    // --- IsOwnedByRequester: does a specific requester field the party ---

    [Fact]
    public void Requester_OwnsPartyItOwns()
    {
        Assert.True(BattleTroopReserveBuilder.IsOwnedByRequester(owningController: "A", requesterController: "A", requesterIsHost: false));
    }

    [Fact]
    public void Requester_DoesNotOwnAnotherPlayersParty_EvenIfHost()
    {
        // A party owned by player A is not fielded by anyone else — being the host does not override a player owner.
        Assert.False(BattleTroopReserveBuilder.IsOwnedByRequester(owningController: "A", requesterController: "B", requesterIsHost: true));
    }

    [Fact]
    public void HostFieldsPartyWithNoPlayerOwner()
    {
        Assert.True(BattleTroopReserveBuilder.IsOwnedByRequester(owningController: null, requesterController: "X", requesterIsHost: true));
    }

    [Fact]
    public void NonHostDoesNotFieldPartyWithNoPlayerOwner()
    {
        // An AI party with no player owner is the host's; a non-host requester must not also field it (no double-spawn).
        Assert.False(BattleTroopReserveBuilder.IsOwnedByRequester(owningController: null, requesterController: "X", requesterIsHost: false));
    }
}
