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
        // The setter takes the ship out of the losing party's list, so RemoveParty at its finalize no longer destroys it.
        ship.Owner = null;
        held[ship] = new HeldShip(looter, UtcNow() + FallbackHoldTime);
    }

    public void Release(PartyBase looter, IReadOnlyCollection<Ship> offered)
    {
        foreach (var entry in held.Where(entry => entry.Value.Looter == looter).ToList())
        {
            var ship = entry.Key;
            held.Remove(ship);
            if (offered.Contains(ship)) continue;

            DestroyIfUnclaimed(ship, entry.Value.Looter);
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
                continue;
            }

            if (now < entry.Value.ExpiresUtc && !IsLooterGone(entry.Value.Looter)) continue;

            held.Remove(ship);
            DestroyIfUnclaimed(ship, entry.Value.Looter);
        }
    }

    // Taken through the loot screen, or already destroyed (its discard, a finalize).
    private bool IsClaimedOrGone(Ship ship) => ship.Owner != null || !objectManager.TryGetId(ship, out _);

    // Destroys a held ship nobody took, through the vanilla destroy that the ship sync replicates.
    private void DestroyIfUnclaimed(Ship ship, PartyBase looter)
    {
        if (IsClaimedOrGone(ship)) return;

        objectManager.TryGetId(ship, out var shipId);
        Logger.Information("[NavalSync] Destroying unclaimed loot ship {ShipId} of {Looter}", shipId, looter?.Id);
        DestroyShipAction.Apply(ship);
    }

    // A looter whose party is gone or whose player is disconnected will never close its loot screen.
    private bool IsLooterGone(PartyBase looter) =>
        looter?.MobileParty == null || playerManager.IsOwnerOfPartyDisconnected(looter.MobileParty);

    // The player party a ship was looted for and when its fallback hold runs out.
    private readonly struct HeldShip
    {
        public HeldShip(PartyBase looter, DateTime expiresUtc)
        {
            Looter = looter;
            ExpiresUtc = expiresUtc;
        }

        public PartyBase Looter { get; }
        public DateTime ExpiresUtc { get; }
    }
}
