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
    /// Applies the held fates of the ships <paramref name="party"/> owns and of the <paramref name="capturedShipIds"/> its player
    /// captured in the mission, as that player departs the battle with them.
    /// </summary>
    void ApplyParty(MapEvent mapEvent, PartyBase party, IReadOnlyCollection<string> capturedShipIds);

    /// <summary>Applies every held fate of the battle.</summary>
    void ApplyAll(MapEvent mapEvent);
}

/// <inheritdoc cref="IBattleShipFateApplier"/>
public class BattleShipFateApplier : IBattleShipFateApplier
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleShipFateApplier>();

    // Smaller HP differences are float noise, so the ship keeps its value and no write replicates.
    private const float Tolerance = 0.01f;

    // Vanilla's Boatswain XP per point of mitigated ship damage (NavalSkillLevellingManager.OnShipDamaged), for the log only.
    private const float BoatswainXpPerMitigatedPoint = 0.1f;

    private readonly IBattleShipFateLedger ledger;
    private readonly IObjectManager objectManager;

    public BattleShipFateApplier(IBattleShipFateLedger ledger, IObjectManager objectManager)
    {
        this.ledger = ledger;
        this.objectManager = objectManager;
    }

    public void ApplyParty(MapEvent mapEvent, PartyBase party, IReadOnlyCollection<string> capturedShipIds) =>
        Apply(mapEvent, (shipId, ship) => ship.Owner == party || capturedShipIds.Contains(shipId));

    public void ApplyAll(MapEvent mapEvent) => Apply(mapEvent, (shipId, ship) => true);

    // Resolves each held ship once. A fate whose ship is already gone (destroyed by a finalize, a loot discard) or ownerless is
    // taken and dropped, so the ledger never keeps it; vanilla's destroy and every HP write expect an owned ship.
    private void Apply(MapEvent mapEvent, Func<string, Ship, bool> selects)
    {
        if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId) || !ledger.HasFates(mapEventId)) return;

        var ships = new Dictionary<string, Ship>();
        var fates = ledger.Take(mapEventId, fate =>
        {
            if (!objectManager.TryGetObject<Ship>(fate.CampaignShipId, out var ship) || ship.Owner == null) return true;
            if (!selects(fate.CampaignShipId, ship)) return false;

            ships[fate.CampaignShipId] = ship;
            return true;
        });

        foreach (var fate in fates)
        {
            if (ships.TryGetValue(fate.CampaignShipId, out var ship)) ApplyFate(ship, fate);
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

        var boatswain = ship.Owner.MobileParty?.GetEffectiveRoleHolder(PartyRole.FirstMate);
        Logger.Information("[NavalSync] Boatswain {Hero} of {Owner} gains {Xp} skill XP for ship {ShipId} ({MitigatedDamage} mitigated damage)",
            boatswain?.StringId ?? "none", ship.Owner.Id, fate.MitigatedDamage * BoatswainXpPerMitigatedPoint, fate.CampaignShipId,
            fate.MitigatedDamage);
        SkillLevelingManager.OnShipDamaged(ship, fate.MitigatedDamage, 0f);
    }
}
