using Common.Logging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Ships.Data;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.Ships;

/// <summary>
/// [Server, game thread] Writes held naval ship fates to the campaign ships with patches live, so every write replicates: a
/// sunk ship is destroyed, any other ship takes its battle HP and sail HP.
/// </summary>
public interface IBattleShipFateApplier
{
    /// <summary>Applies the held fates of the ships <paramref name="party"/> owns, as its player departs the battle. How many applied.</summary>
    int ApplyParty(MapEvent mapEvent, PartyBase party);

    /// <summary>Applies every held fate of the battle. How many applied.</summary>
    int ApplyAll(MapEvent mapEvent);
}

/// <inheritdoc cref="IBattleShipFateApplier"/>
public class BattleShipFateApplier : IBattleShipFateApplier
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleShipFateApplier>();

    // Smaller HP differences are float noise, so the ship keeps its value and no write replicates.
    private const float Tolerance = 0.01f;

    private readonly IBattleShipFateLedger ledger;
    private readonly IObjectManager objectManager;

    public BattleShipFateApplier(IBattleShipFateLedger ledger, IObjectManager objectManager)
    {
        this.ledger = ledger;
        this.objectManager = objectManager;
    }

    public int ApplyParty(MapEvent mapEvent, PartyBase party) => Apply(mapEvent, ship => ship.Owner == party);

    public int ApplyAll(MapEvent mapEvent) => Apply(mapEvent, ship => true);

    // A fate whose ship is gone is taken too, so the ledger never keeps it; applying it then finds nothing to write.
    private int Apply(MapEvent mapEvent, Func<Ship, bool> selects)
    {
        if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId) || !ledger.HasFates(mapEventId)) return 0;

        var fates = ledger.Take(mapEventId, fate => !objectManager.TryGetObject<Ship>(fate.CampaignShipId, out var ship) || selects(ship));
        int applied = 0;
        foreach (var fate in fates)
        {
            if (ApplyFate(fate)) applied++;
        }

        return applied;
    }

    private bool ApplyFate(BattleShipFate fate)
    {
        if (!objectManager.TryGetObjectWithLogging<Ship>(fate.CampaignShipId, out var ship)) return false;

        // A ship with no owner was already destroyed; vanilla's destroy and every HP write expect an owned ship.
        if (ship.Owner == null) return false;

        if (fate.Sunk)
        {
            Logger.Information("[NavalSync] Ship {ShipId} of {Owner} sank in battle; destroying it", fate.CampaignShipId, ship.Owner.Id);
            DestroyShipAction.Apply(ship);
            return true;
        }

        float hitPoints = MathF.Clamp(fate.HitPoints, 0f, ship.MaxHitPoints);
        float sailHitPoints = MathF.Clamp(fate.SailHitPoints, 0f, ship.MaxSailHitPoints);
        if (Math.Abs(ship.HitPoints - hitPoints) > Tolerance) ship.HitPoints = hitPoints;
        if (Math.Abs(ship.SailHitPoints - sailHitPoints) > Tolerance) ship.SailHitPoints = sailHitPoints;
        Logger.Information("[NavalSync] Ship {ShipId} of {Owner} keeps its battle condition: hp {HitPoints}, sail {SailHitPoints}",
            fate.CampaignShipId, ship.Owner.Id, ship.HitPoints, ship.SailHitPoints);
        return true;
    }
}
