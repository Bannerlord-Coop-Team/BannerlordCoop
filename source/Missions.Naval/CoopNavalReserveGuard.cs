using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using Missions.Battles;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// Ends a coop naval battle whose own troop reserve is not in place when the naval spawn logic pulls its troops.
/// <c>DefaultNavalMissionAgentSpawnLogic</c> pulls every troop once, in <c>EarlyStart</c>, with no later retry,
/// so there is no hold like the land spawn handler's: the battle fails clearly instead.
/// </summary>
public class CoopNavalReserveGuard : MissionLogic
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopNavalReserveGuard>();

    private readonly CoopTroopSupplier ownSupplier;
    private readonly BattleSideEnum playerSide;
    private readonly IMessageBroker messageBroker;

    public CoopNavalReserveGuard(CoopTroopSupplier ownSupplier, BattleSideEnum playerSide, IMessageBroker messageBroker)
    {
        this.ownSupplier = ownSupplier;
        this.playerSide = playerSide;
        this.messageBroker = messageBroker;
    }

    public bool ReservePresent { get; private set; }

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

        messageBroker.Publish(this, new SendInformationMessage(CoopBattleMissionSpawnHandler.InvalidPlayerReserveMessage));
        Mission.EndMission();
    }
}
