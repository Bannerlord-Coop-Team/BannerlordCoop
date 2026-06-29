namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>
/// Pure rules for which controller fields (spawns + deploys) a battle party's troops — the deployment-authority
/// rules behind "army leader deploys the army" (#3). Kept free of game types so they are unit-testable; the
/// <see cref="BattleTroopReserveBuilder"/> resolves the controller ids (a party's own player, the army leader)
/// from the campaign objects and applies these.
/// <para>
/// Increment 1 scope: a party owned by a player is fielded by that player; an AI party in a player-led army is
/// fielded by that army leader; otherwise no player fields it (the host does). Players who are MEMBERS of an
/// army another player leads still keep their own party here — the leader fielding teammates' own parties and
/// heroes is increment 2 (which flips the precedence below to army-leader-wins and adds hero adoption).
/// </para>
/// </summary>
public static class BattleDeploymentAuthority
{
    /// <summary>
    /// The controller that owns a party's reserve, or null if no connected player does (so the host fields it).
    /// A party's own player wins; an AI party (no own player) in a player-led army falls to that army leader.
    /// </summary>
    public static string ResolveOwningController(string partyOwnerController, string armyLeaderController)
        => partyOwnerController ?? armyLeaderController;

    /// <summary>
    /// Whether <paramref name="requesterController"/> fields the party: it is the owning controller, or — when
    /// no player owns it — the requester is the host.
    /// </summary>
    public static bool IsOwnedByRequester(string owningController, string requesterController, bool requesterIsHost)
        => owningController != null ? owningController == requesterController : requesterIsHost;
}
