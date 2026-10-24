using Common.Logging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Ships;

/// <summary>
/// [Server, game thread] Keeps the ships a battle gave a player as loot alive until the player's loot screen closes: each
/// leaves the losing party at once, so its finalize can't destroy it, and one nobody took is destroyed once the screen closes.
/// </summary>
public interface ILootShipHold
{
    /// <summary>Detaches <paramref name="ship"/> from its losing owner, a replicated owner write with no destroy, and holds it for <paramref name="looter"/>.</summary>
    void Hold(Ship ship, PartyBase looter);

    /// <summary>
    /// The looter's ship loot screen closed and its discard listed <paramref name="offered"/>, which LootShipsHandler takes care
    /// of: those are forgotten, and any other ship still held unclaimed for the looter is destroyed.
    /// </summary>
    void Release(PartyBase looter, IReadOnlyCollection<Ship> offered);

    /// <summary>
    /// Forgets the taken or gone ships, and destroys each one nobody took once its looter disconnected or, for a looter whose
    /// screen never closed, the long fallback ran out.
    /// </summary>
    void Sweep();
}

/// <inheritdoc cref="ILootShipHold"/>
public class LootShipHold : ILootShipHold
{
    private static readonly ILogger Logger = LogManager.GetLogger<LootShipHold>();

    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    // Game thread only: the results commit holds ships, a loot discard releases them and the campaign tick sweeps them.
    private readonly Dictionary<Ship, HeldShip> held = new Dictionary<Ship, HeldShip>();

    // Takes the object manager that names the held ships and the player manager that tells a looter's disconnect.
    public LootShipHold(IObjectManager objectManager, IPlayerManager playerManager)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
    }

    /// <summary>
    /// How long a connected looter's ships stay held if its loot screen never closes. The ship screen comes after the troop and
    /// item loot and any conversations, so this is only a backstop; the discard releases them.
    /// </summary>
    internal TimeSpan FallbackHoldTime { get; set; } = TimeSpan.FromHours(2);

    /// <summary>The sweep's clock (tests move it).</summary>
    internal Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

    public void Hold(Ship ship, PartyBase looter)
    {
        objectManager.TryGetId(ship, out var shipId);
        Logger.Information("[NavalSync] Holding loot ship {ShipId} of {Loser} for {Looter} until its loot screen closes",
            shipId, ship.Owner?.Id, looter.Id);

        // The setter takes the ship out of the losing party's list, so RemoveParty at its finalize no longer destroys it.
        ship.Owner = null;
        held[ship] = new HeldShip(shipId, looter, UtcNow() + FallbackHoldTime);
    }

    public void Release(PartyBase looter, IReadOnlyCollection<Ship> offered)
    {
        foreach (var entry in held.Where(entry => entry.Value.Looter == looter).ToList())
        {
            var ship = entry.Key;
            held.Remove(ship);
            if (offered.Contains(ship) || IsClaimedOrGone(ship))
            {
                LogReleased(entry.Value, ReleaseOutcome(ship));
                continue;
            }

            DestroyUnclaimed(ship, entry.Value, "its loot screen closed without it");
        }
    }

    public void Sweep()
    {
        if (held.Count == 0) return;

        var now = UtcNow();
        foreach (var entry in held.ToList())
        {
            var ship = entry.Key;
            if (IsClaimedOrGone(ship))
            {
                held.Remove(ship);
                LogReleased(entry.Value, ReleaseOutcome(ship));
                continue;
            }

            if (now < entry.Value.ExpiresUtc && !IsLooterGone(entry.Value.Looter)) continue;

            held.Remove(ship);
            DestroyUnclaimed(ship, entry.Value, IsLooterGone(entry.Value.Looter) ? "its looter left or disconnected" : "the fallback hold ran out");
        }
    }

    // Taken through the loot screen, or already destroyed (its discard, a finalize).
    private bool IsClaimedOrGone(Ship ship) => ship.Owner != null || !objectManager.TryGetId(ship, out _);

    // What became of a released ship: taken by a party, destroyed already, or left ownerless for the discard to destroy.
    private string ReleaseOutcome(Ship ship)
    {
        if (ship.Owner != null) return "taken by " + ship.Owner.Id;

        return objectManager.TryGetId(ship, out _) ? "discarded by its loot screen" : "already destroyed (discarded)";
    }

    // One line per ship leaving the hold without the hold destroying it.
    private static void LogReleased(HeldShip heldShip, string outcome) =>
        Logger.Information("[NavalSync] Released loot ship {ShipId} held for {Looter}: {Outcome}", heldShip.ShipId, heldShip.Looter.Id, outcome);

    // Destroys a held ship nobody took (the callers checked it is unclaimed), through the vanilla destroy the ship sync replicates.
    private static void DestroyUnclaimed(Ship ship, HeldShip heldShip, string reason)
    {
        Logger.Information("[NavalSync] Destroying unclaimed loot ship {ShipId} held for {Looter}: {Reason}", heldShip.ShipId, heldShip.Looter.Id, reason);
        DestroyShipAction.Apply(ship);
    }

    // A looter whose party was removed (a deleted player's too) or whose player is disconnected will never close its loot
    // screen; RemoveParty only clears IsActive, and a deleted player no longer reads as disconnected.
    private bool IsLooterGone(PartyBase looter) =>
        !looter.MobileParty.IsActive || playerManager.IsOwnerOfPartyDisconnected(looter.MobileParty);

    // The held ship's id (kept for the log once it is destroyed), the player party it was looted for and when its hold runs out.
    private readonly struct HeldShip
    {
        // Assigns the held ship's fields.
        public HeldShip(string shipId, PartyBase looter, DateTime expiresUtc)
        {
            ShipId = shipId;
            Looter = looter;
            ExpiresUtc = expiresUtc;
        }

        public string ShipId { get; }
        public PartyBase Looter { get; }
        public DateTime ExpiresUtc { get; }
    }
}
