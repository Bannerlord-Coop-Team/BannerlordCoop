using Common.Logging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Ships.Data;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.Ships;

/// <summary>
/// [Server, game thread] Writes held naval ship fates to the campaign ships with patches live, so every write replicates: the
/// owner's Boatswain gains the battle's XP, then a sunk ship is destroyed and any other takes its battle HP and sail HP.
/// </summary>
public interface IBattleShipFateApplier
{
    /// <summary>
    /// Applies the held fates of the ships <paramref name="party"/> owns, except those another player captured, and of the ships
    /// its player <paramref name="controllerId"/> captured in the mission, as that player departs the battle with them.
    /// </summary>
    void ApplyParty(MapEvent mapEvent, PartyBase party, string controllerId);

    /// <summary>Applies every held fate of the battle.</summary>
    void ApplyAll(MapEvent mapEvent);
}

/// <inheritdoc cref="IBattleShipFateApplier"/>
public class BattleShipFateApplier : IBattleShipFateApplier
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleShipFateApplier>();

    // Smaller HP differences are float noise, so the ship keeps its value and no write replicates.
    private const float Tolerance = 0.01f;

    private readonly IBattleShipFateLedger ledger;
    private readonly IObjectManager objectManager;

    // Takes the ledger holding the reported fates and the object manager that resolves their ships.
    public BattleShipFateApplier(IBattleShipFateLedger ledger, IObjectManager objectManager)
    {
        this.ledger = ledger;
        this.objectManager = objectManager;
    }

    // A capture never changes the campaign owner, so the owner's departure skips a ship another player captured: that one leaves
    // with its captor, whose departure (or the commit or finalize) applies its fate.
    public void ApplyParty(MapEvent mapEvent, PartyBase party, string controllerId)
    {
        if (!TryGetHeldBattle(mapEvent, out var mapEventId)) return;

        var captured = ledger.CapturedBy(mapEventId, controllerId);
        Apply(mapEventId, (shipId, ship) =>
            captured.Contains(shipId) || (ship.Owner == party && ledger.CaptorOf(mapEventId, shipId) == null));
    }

    // Applies every fate the battle holds.
    public void ApplyAll(MapEvent mapEvent)
    {
        if (!TryGetHeldBattle(mapEvent, out var mapEventId)) return;

        Apply(mapEventId, (shipId, ship) => true);
    }

    // The map event's registry id, when the ledger holds fates of it.
    private bool TryGetHeldBattle(MapEvent mapEvent, out string mapEventId)
    {
        mapEventId = null;
        return mapEvent != null && objectManager.TryGetId(mapEvent, out mapEventId) && ledger.HasFates(mapEventId);
    }

    // Takes the selected fates and writes them. A fate whose ship is already gone (destroyed by a finalize, a loot discard) or
    // ownerless is taken and dropped, so the ledger never keeps it; vanilla's destroy and every HP write expect an owned ship.
    private void Apply(string mapEventId, Func<string, Ship, bool> selects)
    {
        var fates = ledger.Take(mapEventId, fate => !TryGetOwnedShip(fate, out var ship) || selects(fate.CampaignShipId, ship));

        foreach (var fate in fates)
        {
            if (TryGetOwnedShip(fate, out var ship)) TryApplyFate(ship, fate);
        }
    }

    // The fate's campaign ship, while it is registered and has an owner.
    private bool TryGetOwnedShip(BattleShipFate fate, out Ship ship) =>
        objectManager.TryGetObject(fate.CampaignShipId, out ship) && ship.Owner != null;

    // The fates already left the ledger, so a throw from one ship's writes must not skip the others or, at the commit, the
    // battle's results.
    private static void TryApplyFate(Ship ship, BattleShipFate fate)
    {
        try
        {
            ApplyFate(ship, fate);
        }
        catch (Exception e)
        {
            Logger.Error(e, "[NavalSync] Applying the fate of ship {ShipId} failed partway; it is not retried", fate.CampaignShipId);
        }
    }

    // Grants the fate's XP, then destroys a sunk ship or writes the survivor's battle HP.
    private static void ApplyFate(Ship ship, BattleShipFate fate)
    {
        GrantBoatswainXp(ship, fate);
        if (fate.Sunk)
        {
            Logger.Information("[NavalSync] Ship {ShipId} of {Owner} sank in battle; destroying it (hp {HitPoints}/{MaxHitPoints}, sail {SailHitPoints}/{MaxSailHitPoints})",
                fate.CampaignShipId, ship.Owner.Id, ship.HitPoints, ship.MaxHitPoints, ship.SailHitPoints, ship.MaxSailHitPoints);
            DestroyShipAction.Apply(ship);
            return;
        }

        float oldHitPoints = ship.HitPoints;
        float oldSailHitPoints = ship.SailHitPoints;
        float hitPoints = MathF.Clamp(fate.HitPoints, 0f, ship.MaxHitPoints);
        float sailHitPoints = MathF.Clamp(fate.SailHitPoints, 0f, ship.MaxSailHitPoints);
        if (Math.Abs(oldHitPoints - hitPoints) > Tolerance) ship.HitPoints = hitPoints;
        if (Math.Abs(oldSailHitPoints - sailHitPoints) > Tolerance) ship.SailHitPoints = sailHitPoints;
        Logger.Information("[NavalSync] Ship {ShipId} of {Owner} keeps its battle condition: hp {OldHitPoints} -> {HitPoints}, sail {OldSailHitPoints} -> {SailHitPoints}",
            fate.CampaignShipId, ship.Owner.Id, oldHitPoints, ship.HitPoints, oldSailHitPoints, ship.SailHitPoints);
    }

    // Vanilla's per-hit skill call with the battle's mitigated damage as raw and none as final, which grants the owner party's
    // Boatswain (its first mate) the same 0.1 XP per mitigated point (NavalSkillLevellingManager.OnShipDamaged), once per battle.
    private static void GrantBoatswainXp(Ship ship, BattleShipFate fate)
    {
        if (fate.MitigatedDamage <= 0f) return;

        Logger.Information("[NavalSync] Ship {ShipId} of {Owner} grants its Boatswain XP for {MitigatedDamage} mitigated damage",
            fate.CampaignShipId, ship.Owner.Id, fate.MitigatedDamage);
        SkillLevelingManager.OnShipDamaged(ship, fate.MitigatedDamage, 0f);
    }
}
