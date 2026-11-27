using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.TroopSupply;
using Missions.Battles;
using Serilog;
using System;
using TaleWorlds.Core;

namespace Missions.Naval;

/// <summary>
/// Adds the own-reserve check to <see cref="NavalSetupGuard"/> for coop naval battles: a reserve missing when the spawn
/// logic pulls it once in <c>EarlyStart</c> (no retry, so no hold like the land spawn handler's) ends the mission.
/// </summary>
public class CoopNavalSetupGuard : NavalSetupGuard
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopNavalSetupGuard>();

    private readonly CoopTroopSupplier ownSupplier;
    private readonly BattleSideEnum playerSide;

    public CoopNavalSetupGuard(CoopTroopSupplier ownSupplier, BattleSideEnum playerSide, IMessageBroker messageBroker)
        : base(messageBroker)
    {
        this.ownSupplier = ownSupplier;
        this.playerSide = playerSide;
    }

    public bool ReservePresent { get; private set; }

    // Checks the own reserve as the spawn logic pulls it; a missing one ends the mission at its first tick.
    public override void EarlyStart()
    {
        base.EarlyStart();
        ReservePresent = ownSupplier.IsPopulated && ownSupplier.GetRemainingForParty(ownSupplier.PlayerPartyId) > 0;
        if (ReservePresent)
        {
            Logger.Information("[NavalBattle] Own reserve present at naval spawn: side={Side} party={PartyId} troops={Troops}",
                playerSide, ownSupplier.PlayerPartyId, ownSupplier.GetRemainingForParty(ownSupplier.PlayerPartyId));
            return;
        }

        Logger.Error("[NavalBattle] Own reserve missing at naval spawn: side={Side} populated={Populated} party={PartyId}; ending the mission",
            playerSide, ownSupplier.IsPopulated, ownSupplier.PlayerPartyId);

        // An unpopulated supplier reports troops remaining forever, which would spin the vanilla initial pull.
        if (!ownSupplier.IsPopulated)
            ownSupplier.SetReserve(Array.Empty<PartyReserve>(), 0, 0, 0);

        End(NavalSetupEndReason.MissingReserve);
    }

    // A missing reserve has its own player message; the other reasons are no fault of the player's party.
    protected override string MessageFor(NavalSetupEndReason reason) =>
        reason == NavalSetupEndReason.MissingReserve ? CoopBattleMissionSpawnHandler.InvalidPlayerReserveMessage : base.MessageFor(reason);
}
