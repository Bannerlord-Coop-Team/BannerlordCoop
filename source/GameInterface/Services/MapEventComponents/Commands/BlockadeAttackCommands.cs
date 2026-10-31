using Common;
using Common.Commands;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.Settlements.Interfaces;
using System.Linq;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEventComponents.Commands;

public class BlockadeAttackCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    // coop.debug.naval.attack_blockade town_ES1
    /// <summary>Has the nearest AI lord that can help a blockaded settlement's defenders attack its blockade from the sea.</summary>
    public sealed class AttackBlockadeCoopCommand : ICoopCommand
    {
        private const int DefaultShipCount = 2;

        private readonly ISettlementInterface settlementInterface;

        public AttackBlockadeCoopCommand(ISettlementInterface settlementInterface)
        {
            this.settlementInterface = settlementInterface;
        }

        public string Prefix => "coop.debug.naval";

        public string Name => "attack_blockade";

        public string Description => "Has the nearest AI lord that can help the defenders attack a settlement's blockade from the sea, " +
            "giving it ships of the blockade leader's flagship hull if it has none.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlement_id", "The blockaded settlement.", true),
            new ExpectedArgs("ships", $"How many ships an AI lord with none gets, {DefaultShipCount} by default.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");

            if (!ModInformation.IsNavalDlcActive)
                return Failed("NavalDLC is not active on the server.");

            if (args.Count < 1 || args.Count > 2)
                return Failed("Expected a settlement id and an optional ship count.");

            int shipCount = DefaultShipCount;
            if (args.Count == 2 && (!int.TryParse(args[1], out shipCount) || shipCount < 1))
                return Failed("The ship count must be a positive number.");

            var settlement = Settlement.Find(args[0]);
            if (settlement == null)
                return Failed($"No settlement has id {args[0]}.");

            var siegeEvent = settlement.SiegeEvent;
            if (siegeEvent?.IsBlockadeActive != true)
                return Failed($"{settlement.StringId} has no active blockade.");

            var blockadeLeader = siegeEvent.BesiegerCamp.LeaderParty;
            if (blockadeLeader.MapEvent != null)
                return Failed($"The blockade leader {blockadeLeader.StringId} is already in a battle.");

            var lord = MobileParty.AllLordParties
                .Where(party => party.IsActive && !party.IsPlayerParty() && party.LeaderHero != null && party.CurrentSettlement == null &&
                    party.MapEvent == null && party.BesiegerCamp == null && party.Army == null && party.MemberRoster.TotalManCount > 0 &&
                    siegeEvent.CanPartyJoinSide(party.Party, BattleSideEnum.Defender))
                .OrderBy(party => party.Position.ToVec2().DistanceSquared(settlement.Position.ToVec2()))
                .FirstOrDefault();
            if (lord == null)
                return Failed($"No free AI lord can help the defenders of {settlement.StringId}.");

            if (lord.Ships.Count == 0)
            {
                var hull = blockadeLeader.Party.FlagShip?.ShipHull;
                if (hull == null)
                    return Failed($"{lord.StringId} has no ships and the blockade leader has no flagship to copy.");

                for (int i = 0; i < shipCount; i++)
                {
                    ChangeShipOwnerAction.ApplyByProduction(lord.Party, new Ship(hull));
                }
            }

            settlementInterface.PartySetSail(lord, settlement);

            // Same as EncounterManager.StartSettlementEncounter for a party defending the settlement from the sea
            var flags = new BattleCreationFlags(
                forceRaid: false,
                forceSallyOut: false,
                forceVolunteers: false,
                forceSupplies: false,
                isSallyOutAmbush: false,
                forceBlockadeAttack: true,
                forceBlockadeSallyOutAttack: false,
                forceHideoutSendTroops: false);
            if (MapEventBattleFactory.CreateMapEvent(lord.Party, blockadeLeader.Party, flags) == null)
                return Failed($"The blockade attack by {lord.StringId} was refused, check the server log.");

            if (!siegeEvent.IsBlockadeActive)
                return Succeeded($"{lord.StringId} was strong enough to lift the blockade of {settlement.StringId} without a battle.");

            return Succeeded($"{lord.StringId} with {lord.Ships.Count} ship(s) attacked the blockade of {settlement.StringId} " +
                $"led by {blockadeLeader.StringId}.");
        }
    }
}
