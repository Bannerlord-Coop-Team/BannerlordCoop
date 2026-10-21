using Common.Commands;
using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.CampaignSystem.Settlements.Settlement;
namespace GameInterface.Services.Template.Commands;

internal class SettlementCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

#if DEBUG
    private static CastleTeleportSnapshot castleTeleportSnapshot;

    private sealed class CastleTeleportSnapshot
    {
        public Campaign Campaign { get; }
        public MobileParty Party { get; }
        public CampaignVec2 Position { get; }

        public CastleTeleportSnapshot(Campaign campaign, MobileParty party, CampaignVec2 position)
        {
            Campaign = campaign;
            Party = party;
            Position = position;
        }
    }
#endif

    public sealed class EnterRandomCastleCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "enter_random_castle";

        public string Description => "Enters random castle for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Client;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("castleId", "Castle name or id; quote names with spaces.", isRequired: false),
        };

        private readonly ICommandObjectResolver resolver;

        // Injects the resolver that turns command arguments into game objects.
        public EnterRandomCastleCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");

            var castles = Campaign.Current.CampaignObjectManager.Settlements.Where(settlement => settlement.IsCastle).ToArray();
            var castle = strings.Count == 0 ? castles[new Random().Next(castles.Length)] : null;
            if (castle == null && resolver.TryResolve<Settlement>(strings[0], castles, s => s.Name?.ToString(), out castle, out var castleError) == false)
                return Failed(castleError);

            EncounterManager.StartSettlementEncounter(MobileParty.MainParty, castle);

            return Succeeded($"Requested settlement encounter with {castle.Name} ({castle.StringId}).");

        }
    }

#if DEBUG
    public sealed class TeleportMainPartyToCastleCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "teleport_main_party_to_castle";

        public string Description => "Runs main party to castle for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Client;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("castleId", "Castle name or id; quote names with spaces."),
        };

        private readonly ICommandObjectResolver resolver;

        // Injects the resolver that turns command arguments into game objects.
        public TeleportMainPartyToCastleCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");

            var castles = Campaign.Current.CampaignObjectManager.Settlements.Where(settlement => settlement.IsCastle).ToArray();
            if (resolver.TryResolve<Settlement>(strings[0], castles, s => s.Name?.ToString(), out var castle, out var castleError) == false)
                return Failed(castleError);

            var mainParty = MobileParty.MainParty;
            if (mainParty == null)
                return Failed("Failed: no main party.");
            if (mainParty.CurrentSettlement != null || mainParty.MapEvent != null || PlayerEncounter.Current != null)
                return Failed("Leave the active settlement or map event before teleporting.");
            if (TryGetCurrentCastleTeleportSnapshot(mainParty, out _))
                return Failed("Restore the previous castle teleport before starting another one.");

            var originalPosition = mainParty.Position;
            castleTeleportSnapshot = new CastleTeleportSnapshot(Campaign.Current, mainParty, originalPosition);
            try
            {
                mainParty.Position = castle.GatePosition;
                MessageBroker.Instance.Publish(
                    mainParty,
                    new PartyBehaviorChangeAttempted(
                        mainParty,
                        forcePosition: true,
                        isCurrentlyAtSea: false,
                        resetMovementToHold: true));
            }
            finally
            {
                // The later forced-position echo proves the dedicated server applied the request.
                mainParty.Position = originalPosition;
            }

            return Succeeded($"Requested authoritative teleport to {castle.Name} ({castle.StringId}) gate " +
                $"at {castle.GatePosition.X:R},{castle.GatePosition.Y:R}.");

        }
    }

    public sealed class RestoreMainPartyCastleTeleportCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "restore_main_party_castle_teleport";

        public string Description => "Restores main party castle teleport for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Client;

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");

            var mainParty = MobileParty.MainParty;
            if (mainParty == null)
                return Failed("Failed: no main party.");
            if (!TryGetCurrentCastleTeleportSnapshot(mainParty, out var snapshot))
                return Failed("No castle teleport is waiting to be restored.");
            if (mainParty.CurrentSettlement != null || mainParty.MapEvent != null || PlayerEncounter.Current != null)
                return Failed("Leave the active settlement or map event before restoring the teleport.");

            var currentPosition = mainParty.Position;
            var restorePosition = snapshot.Position;
            castleTeleportSnapshot = null;
            try
            {
                mainParty.Position = restorePosition;
                MessageBroker.Instance.Publish(
                    mainParty,
                    new PartyBehaviorChangeAttempted(
                        mainParty,
                        forcePosition: true,
                        isCurrentlyAtSea: restorePosition.IsOnLand == false,
                        resetMovementToHold: true));
            }
            finally
            {
                mainParty.Position = currentPosition;
            }

            return Succeeded($"Requested authoritative castle-teleport restoration to " +
                $"{restorePosition.X:R},{restorePosition.Y:R}.");

        }
    }

    private static bool TryGetCurrentCastleTeleportSnapshot(
        MobileParty mainParty,
        out CastleTeleportSnapshot snapshot)
    {
        snapshot = castleTeleportSnapshot;
        if (snapshot == null)
            return false;

        if (ReferenceEquals(snapshot.Campaign, Campaign.Current) &&
            ReferenceEquals(snapshot.Party, mainParty))
            return true;

        castleTeleportSnapshot = null;
        snapshot = null;
        return false;
    }
#endif

    public sealed class GetTownNameCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "get_town_name";

        public string Description => "Gets town name for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementId", "Settlement name or id; quote names with spaces."),
        };

        private readonly ICommandObjectResolver resolver;

        // Injects the resolver that turns command arguments into game objects.
        public GetTownNameCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {

            if (resolver.TryResolveSettlement(strings.Single(), out var settlement, out var settlementError) == false)
                return Failed(settlementError);

            return Succeeded($"Settlement Name: {settlement.Name}");

        }
    }

    // coop.debug.settlements.set_enemies_spotted town_ES3 45.4
    /// <summary>
    /// Changes the NumberOfEnemiesSpottedAround
    /// </summary>
    /// <param name="args">the settlement and float value</param>
    /// <returns>info that is was succesfull</returns>
    public sealed class SetEnemiesSpottedCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "set_enemies_spotted";

        public string Description => "Sets enemies spotted for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementId", "Settlement name or id; quote names with spaces."),
            new ExpectedArgs("value", "The value."),
        };

        private readonly ICommandObjectResolver resolver;

        // Injects the resolver that turns command arguments into game objects.
        public SetEnemiesSpottedCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This function can only be used by the server");


            string settlementId = args[0];

            if (resolver.TryResolveSettlement(settlementId, out var settlement, out var settlementError) == false)
                return Failed(settlementError);


            if (float.TryParse(args[1], out var num) == false)
            {
                return Failed($"Error setting the value: {args[1]} to a float.");
            }

            settlement.NearbyLandThreatIntensity = num;

            return Succeeded($"Successfully set the Settlement ({settlement.StringId}) NumberOfEnemiesSpottedAround to '{args[1]}'");

        }
    }


    // coop.debug.settlements.set_allies_spotted town_ES3 45.4
    /// <summary>
    /// Changes the NumberOfAlliesSpottedAround
    /// </summary>
    /// <param name="args">the settlement and float value</param>
    /// <returns>info that is was succesful</returns>
    public sealed class SetAlliesSpottedCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "set_allies_spotted";

        public string Description => "Sets allies spotted for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementId", "Settlement name or id; quote names with spaces."),
            new ExpectedArgs("value", "The value."),
        };

        private readonly ICommandObjectResolver resolver;

        // Injects the resolver that turns command arguments into game objects.
        public SetAlliesSpottedCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This function can only be used by the server");


            string settlementId = args[0];

            if (resolver.TryResolveSettlement(settlementId, out var settlement, out var settlementError) == false)
                return Failed(settlementError);

            if (float.TryParse(args[1], out var num) == false)
                return Failed($"Error setting the value: {args[1]} to a float.");

            settlement.NearbyLandAllyIntensity = num;

            return Succeeded($"Successfully set the Settlement ({settlement.StringId}) NumberOfAlliesSpottedAround to '{args[1]}'");

        }
    }


    // coop.debug.settlements.set_bribe_paid town_ES3 50.0
    /// <summary>
    /// Changes the BribePaid
    /// </summary>
    /// <param name="args">the settlement and int value</param>
    /// <returns>info that is was succesful</returns>
    public sealed class SetBribePaidCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "set_bribe_paid";

        public string Description => "Sets bribe paid for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementId", "Settlement name or id; quote names with spaces."),
            new ExpectedArgs("value", "The value."),
        };

        private readonly ICommandObjectResolver resolver;

        // Injects the resolver that turns command arguments into game objects.
        public SetBribePaidCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This function can only be used by the server");


            string settlementId = args[0];

            if (resolver.TryResolveSettlement(settlementId, out var settlement, out var settlementError) == false)
                return Failed(settlementError);

            if (int.TryParse(args[1], out var num) == false)
                return Failed($"Error setting the value: {args[1]} to a int.");

            settlement.BribePaid = num;

            return Succeeded($"Successfully set the Settlement ({settlement.StringId}) BribePaid to '{args[1]}'");

        }
    }


    // coop.debug.settlements.set_hit_points town_ES3 50.4
    /// <summary>
    /// Changes the SettlementHitPoints
    /// </summary>
    /// <param name="args">the settlement and float value</param>
    /// <returns>info that is was succesful</returns>
    public sealed class SetHitPointsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "set_hit_points";

        public string Description => "Sets hit points for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementId", "Settlement name or id; quote names with spaces."),
            new ExpectedArgs("value", "The value."),
        };

        private readonly ICommandObjectResolver resolver;

        // Injects the resolver that turns command arguments into game objects.
        public SetHitPointsCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This function can only be used by the server");


            string settlementId = args[0];

            if (resolver.TryResolveSettlement(settlementId, out var settlement, out var settlementError) == false)
                return Failed(settlementError);

            if (float.TryParse(args[1], out var num) == false)
                return Failed($"Error setting the value: {args[1]} to a float.");

            settlement.SettlementHitPoints = num;

            return Succeeded($"Successfully set the Settlement ({settlement.StringId}) SettlementHitPoints to '{args[1]}'");

        }
    }

    // coop.debug.settlements.last_attacker town_ES1 CoopParty
    // coop.debug.settlements.last_attacker town_ES3 lord_2_8_party_1
    /// <summary>
    /// Changes the LastAttackerParty
    /// </summary>
    /// <param name="args">the settlementid and last_attacker</param>
    /// <returns>info that is was succesful</returns>
    public sealed class SetLastAttackerPartyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "last_attacker";

        public string Description => "Runs attacker for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementId", "Settlement name or id; quote names with spaces."),
            new ExpectedArgs("lastAttackerPartyId", "Last attacker party name or id; quote names with spaces."),
        };

        private readonly ICommandObjectResolver resolver;

        // Injects the resolver that turns command arguments into game objects.
        public SetLastAttackerPartyCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This function can only be used by the server");


            string settlementId = args[0];
            string mobilePartyId = args[1];

            if (resolver.TryResolveSettlement(settlementId, out var settlement, out var settlementError) == false)
                return Failed(settlementError);


            if (resolver.TryResolveMobileParty(mobilePartyId, out var mobileParty, out var partyError) == false)
                return Failed(partyError);


            settlement.LastAttackerParty = mobileParty;


            return Succeeded($"Successfully set the Settlement ({settlement.StringId}) MobileParty to '{mobileParty.StringId}'");

        }
    }

    // coop.debug.settlements.list_siege_state
    /// <summary>
    // Lists all the possible siege states
    /// </summary>
    /// <returns>all the siegeStates</returns>
    public sealed class ListSiegeStatesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "list_siege_state";

        public string Description => "Lists siege state for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            StringBuilder sb = new();

            foreach(int i in Enum.GetValues(typeof(Settlement.SiegeState))) {
                sb.AppendLine($"{i}: {Enum.GetName(typeof(Settlement.SiegeState), i)}");
            }
            return Succeeded(sb.ToString());

        }
    }

    // coop.debug.settlements.set_siege_state town_ES1 InTheLordsHall
    /// <summary>
    /// Changes the SiegeState
    /// </summary>
    /// <param name="args">the settlementid and SiegeState</param>
    /// <returns>info that is was succesful</returns>
    public sealed class SetSiegeStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "set_siege_state";

        public string Description => "Sets siege state for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementId", "Settlement name or id; quote names with spaces."),
            new ExpectedArgs("siegeState", "The siege state."),
        };

        private readonly ICommandObjectResolver resolver;

        // Injects the resolver that turns command arguments into game objects.
        public SetSiegeStateCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This function can only be used by the server");


            string settlementId = args[0];
            string siegeState = args[1];

            if (resolver.TryResolveSettlement(settlementId, out var settlement, out var settlementError) == false)
                return Failed(settlementError);

            if (Enum.TryParse<SiegeState>(siegeState, true, out var state) == false)
                return Failed($"{siegeState} was not a valid enum in {nameof(SiegeState)}");


            settlement.CurrentSiegeState = state;


            return Succeeded($"Successfully set the Settlement ({settlement.StringId}) SiegeState to '{siegeState}'");

        }
    }



    // coop.debug.settlements.set_militia town_ES1 45.0
    /// <summary>
    /// Changes the SiegeState
    /// </summary>
    /// <param name="args">the settlementid and float of how many troops (negative or pos)</param>
    /// <returns>info that is was succesful</returns>
    public sealed class SetMiltiiaCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "set_militia";

        public string Description => "Sets militia for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementId", "Settlement name or id; quote names with spaces."),
            new ExpectedArgs("militia", "The militia."),
        };

        private readonly ICommandObjectResolver resolver;

        // Injects the resolver that turns command arguments into game objects.
        public SetMiltiiaCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This function can only be used by the server");


            string settlementId = args[0];
            string militiaFloat = args[1];

            if (resolver.TryResolveSettlement(settlementId, out var settlement, out var settlementError) == false)
                return Failed(settlementError);


            if (float.TryParse(militiaFloat, out var militia) == false)
                return Failed($"Error setting the value: {militiaFloat} to a float.");

            settlement.Militia = militia;


            return Succeeded($"Successfully set the Settlement ({settlement.StringId}) Militia to '{militia}'");

        }
    }


    // coop.debug.settlements.set_garrison_pay_limit town_ES3 23
    /// <summary>
    /// Changes the SiegeState
    /// </summary>
    /// <param name="args">the settlementid and float of how many troops (negative or pos)</param>
    /// <returns>info that is was succesful</returns>
    public sealed class SetGarrisonWageLimitCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "set_garrison_pay_limit";

        public string Description => "Sets garrison pay limit for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementId", "Settlement name or id; quote names with spaces."),
            new ExpectedArgs("payLimit", "The pay limit."),
        };

        private readonly ICommandObjectResolver resolver;

        // Injects the resolver that turns command arguments into game objects.
        public SetGarrisonWageLimitCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This function can only be used by the server");


            string settlementId = args[0];
            string garrisonInt = args[1];

            if (resolver.TryResolveSettlement(settlementId, out var settlement, out var settlementError) == false)
                return Failed(settlementError);


            if (int.TryParse(garrisonInt, out var wageLimit) == false)
                return Failed($"Error setting the value: {garrisonInt} to an int.");

            settlement.SetGarrisonWagePaymentLimit(wageLimit);


            return Succeeded($"Successfully set the Settlement ({settlement.StringId}) GarrisonWagePaymentLimit to '{wageLimit}'");

        }
    }


    // coop.debug.settlements.collect_cache_notables town_ES3
    /// <summary>
    /// Tests collecting of notables to cache.
    /// </summary>
    /// <param name="args">the settlementid </param>
    /// <returns>info that is was successful</returns>
    public sealed class CollectCacheNotablesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "collect_cache_notables";

        public string Description => "Collects cache notables for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementId", "Settlement name or id; quote names with spaces."),
        };

        private readonly ICommandObjectResolver resolver;

        // Injects the resolver that turns command arguments into game objects.
        public CollectCacheNotablesCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This function can only be used by the server");


            string settlementId = args[0];

            if (resolver.TryResolveSettlement(settlementId, out var settlement, out var settlementError) == false)
                return Failed(settlementError);


            settlement.CollectNotablesToCache();


            return Succeeded($"Successfully called settlement.CollectNotablesToCache() for {settlement.StringId}.");

        }
    }



    // Located in Modules\SandBox\ModuleData\settlements.xml
    // POROS EXAMPLE
    // coop.debug.settlements.info town_ES3
    /// <summary>
    /// Gives a bunch of information on a settlement.
    /// </summary>
    /// <param name="args">settlement name</param>
    /// <returns>info about the settlement</returns>
    public sealed class InfoCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "info";

        public string Description => "Shows the relevant state for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementId", "Settlement name or id; quote names with spaces."),
        };

        private readonly ICommandObjectResolver resolver;

        // Injects the resolver that turns command arguments into game objects.
        public InfoCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {


            string settlementId = args.Single();

            if (resolver.TryResolveSettlement(settlementId, out var settlement, out var settlementError) == false)
                return Failed(settlementError);

            StringBuilder sb = new();

            string lastAttackerParty = settlement.LastAttackerParty?.ArmyName.ToString() ?? "None";

            sb.AppendLine($"------------------- SETTLEMENT: {settlement.Name} -------------------");
            sb.AppendLine($"NumberOfEnemiesSpottedAround: '{settlement.NearbyLandThreatIntensity}'");
            sb.AppendLine($"NumberOfAlliesSpottedAround: '{settlement.NearbyLandAllyIntensity}'");
            sb.AppendLine($"BribePaid: {settlement.BribePaid}");
            sb.AppendLine($"SettlementHitPoints: '{settlement.SettlementHitPoints}'");
            sb.AppendLine($"GarrisonWagePaymentLimit: '{settlement.GarrisonWagePaymentLimit}'");
            sb.AppendLine($"LastAttackerParty: '{lastAttackerParty}'");
            sb.AppendLine($"LastThreatTime:  '{settlement.LastThreatTime}'");
            sb.AppendLine($"CurrentSiegeState:   '{settlement.CurrentSiegeState}'");
            sb.AppendLine($"Militia :   '{settlement.Militia}'");
            sb.AppendLine($"LastVisitTimeOfOwner  :   '{settlement.LastVisitTimeOfOwner}'");
            //sb.AppendLine($"ClaimedBy   :   '{settlement.ClaimedBy}'");
            //sb.AppendLine($"ClaimValue    :   '{settlement.ClaimValue}'");
            //sb.AppendLine($"CanBeClaimed     :   '{Convert.ToBoolean(settlement.CanBeClaimed)}'");
            sb.AppendLine($"------------------- SETTLEMENT: {settlement.Name} -------------------");

            return Succeeded(sb.ToString());

        }
    }

    // coop.debug.settlementcomponent.set_owner town_comp_ES3 lord_6_5_party_1
    // Change Poros component owner
    /// <summary>
    /// Changes <see cref="SettlementComponent.Owner"/>
    /// </summary>
    /// <param name="args"><see cref="SettlementComponent"/> id, <see cref="MobileParty"/> or <see cref="Settlement"/> id</param>
    /// <returns>info that is was successful</returns>
    public sealed class SetOwnerCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlement_component";

        public string Name => "set_owner";

        public string Description => "Sets owner for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementComponentId", "Settlement component id, or the name or id of its settlement; quote names with spaces."),
            new ExpectedArgs("mobilePartyId", "Settlement or mobile party name or id; quote names with spaces."),
        };

        private readonly ICommandObjectResolver resolver;
        private readonly IObjectManager objectManager;

        // Injects the resolver and registry used to look up the settlement component argument.
        public SetOwnerCoopCommand(ICommandObjectResolver resolver, IObjectManager objectManager)
        {
            this.resolver = resolver;
            this.objectManager = objectManager;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This function can only be used by the server");


            string partyBaseId = args[1];
            if (TryResolveSettlementComponent(objectManager, resolver, args[0], out var settlementComponent, out var componentError) == false)
                return Failed(componentError);

            var foundSettlement = resolver.TryResolveSettlement(partyBaseId, out var settlement, out var settlementError);
            var foundParty = resolver.TryResolveMobileParty(partyBaseId, out var mobileParty, out var partyError);
            if (foundSettlement && foundParty)
                return Failed($"'{partyBaseId}' matches both Settlement {settlement.Name} ({settlement.StringId}) and MobileParty {mobileParty.Name} ({mobileParty.StringId}); use an id.");
            if (foundSettlement == false && foundParty == false)
                return Failed($"PartyBase '{partyBaseId}' was not found. {settlementError} {partyError}");

            var partyBase = foundSettlement ? settlement.Party : mobileParty.Party;
            var ownerId = foundSettlement ? settlement.StringId : mobileParty.StringId;

            settlementComponent.Owner = partyBase;

            return Succeeded($"Successfully set the SettlementComponent ({resolver.DisplayId(settlementComponent)}) Owner to '{ownerId}'");

        }
    }

    // coop.debug.settlements.capture_by_siege Danustica
    // coop.debug.settlements.capture_by_siege town_EN2 lord_1_1
    // Forcibly transfers a fortification as if captured by siege (ChangeOwnerOfSettlementDetail.BySiege),
    // which is the only path that destroys the old garrison and creates a new one for the new owner.
    // Lets the garrison destroy/recreate + governor-removal replication be tested without staging a
    // real siege. Server only. Settlement is resolved by name or id; the capturer (used as the new
    // owner and the garrison's destroyer) defaults to an enemy-kingdom clan leader with a party, so
    // the resulting fief owner is in a kingdom and the post-siege claimant decision is well-formed.
    public sealed class CaptureBySiegeCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "capture_by_siege";

        public string Description => "Captures by siege for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementNameOrId", "Settlement name or id; quote names with spaces."),
            new ExpectedArgs("capturerHeroId", "Capturer hero name or id; quote names with spaces.", isRequired: false),
        };

        private readonly ICommandObjectResolver resolver;

        // Injects the resolver that turns command arguments into game objects.
        public CaptureBySiegeCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This function can only be used by the server");


            if (resolver.TryResolveSettlement(args[0], out var settlement, out var settlementError) == false)
                return Failed(settlementError);
            if (!settlement.IsFortification)
                return Failed($"'{args[0]}' is not a town or castle");

            Hero capturer;
            if (args.Count >= 2)
            {
                if (resolver.TryResolveHero(args[1], out capturer, out var heroError) == false)
                    return Failed(heroError);
            }
            else
            {
                // Pick an enemy-kingdom clan leader so the capture mirrors a real siege: the new owner's
                // clan is in a kingdom, so the kingdom raises a well-formed SettlementClaimantDecision
                // (a kingdomless owner would produce a malformed one). Prefer one already at war.
                var candidates = Hero.AllAliveHeroes.Where(h =>
                    !h.IsHumanPlayerCharacter &&
                    h.PartyBelongedTo != null &&
                    h.Clan != null &&
                    h.Clan.Leader == h &&
                    h.Clan.Kingdom != null &&
                    h.Clan.Kingdom != settlement.MapFaction).ToList();

                capturer = candidates.FirstOrDefault(h => h.MapFaction.IsAtWarWith(settlement.MapFaction))
                           ?? candidates.FirstOrDefault();
                if (capturer == null)
                    return Failed("No eligible enemy-kingdom clan leader with a party found; pass a hero id as the 2nd arg");
            }

            if (capturer.PartyBelongedTo == null)
                return Failed($"Capturer '{capturer.Name}' has no party; BySiege uses the capturer's party as the garrison destroyer");

            ChangeOwnerOfSettlementAction.ApplyBySiege(capturer, capturer, settlement);

            return Succeeded($"Captured {settlement.Name} by siege; new owner {capturer.Name} ({capturer.MapFaction?.Name})" +
                   Environment.NewLine + FormatOwnerState(settlement));

        }
    }

    public sealed class OwnerStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "owner_state";

        public string Description => "Shows state for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementNameOrId", "Settlement name or id; quote names with spaces."),
        };

        private readonly ICommandObjectResolver resolver;

        // Injects the resolver that turns command arguments into game objects.
        public OwnerStateCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            if (resolver.TryResolveSettlement(args[0], out var settlement, out var settlementError) == false)
                return Failed(settlementError);

            return Succeeded(FormatOwnerState(settlement));

        }
    }

    // Resolves a component by registry id first, else through the settlement named or identified by the query.
    private static bool TryResolveSettlementComponent(
        IObjectManager objectManager,
        ICommandObjectResolver resolver,
        string query,
        out SettlementComponent component,
        out string error)
    {
        error = null;
        // Untyped so a settlement id (town_ES1) misses here without logging a cast error, then resolves below.
        component = objectManager.TryGetObject<object>(query, out var registered) ? registered as SettlementComponent : null;
        if (component != null)
            return true;

        if (resolver.TryResolveSettlement(query, out var settlement, out error) == false)
            return false;

        component = settlement.SettlementComponent;
        if (component != null)
            return true;

        error = $"Settlement {settlement.Name} ({settlement.StringId}) has no settlement component.";
        return false;
    }

    private static string FormatOwnerState(Settlement settlement)
    {
        var role = ModInformation.IsServer ? "SERVER" : "CLIENT";
        var ownerClan = settlement.OwnerClan?.StringId;
        var ownerLeader = settlement.OwnerClan?.Leader?.StringId;
        var structuredState = JsonSerializer.Serialize(new
        {
            role,
            settlement = settlement.StringId,
            ownerClan,
            ownerLeader,
        });

        return $"{role} settlement={settlement.StringId} ownerClan={ownerClan ?? "null"} ownerLeader={ownerLeader ?? "null"}" +
               Environment.NewLine + $"LIVE_TEST_JSON={structuredState}";
    }

    // coop.debug.settlementcomponent.set_gold town_comp_ES3 401021
    // Change Poros component gold
    /// <summary>
    /// Changes <see cref="SettlementComponent.Gold"/>
    /// </summary>
    /// <param name="args"><see cref="SettlementComponent"/> id, amount of gold</param>
    /// <returns>info that is was successful</returns>
    public sealed class SetGoldCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlement_component";

        public string Name => "set_gold";

        public string Description => "Sets gold for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementComponentId", "Settlement component id, or the name or id of its settlement; quote names with spaces."),
            new ExpectedArgs("gold", "The gold."),
        };

        private readonly ICommandObjectResolver resolver;
        private readonly IObjectManager objectManager;

        // Injects the resolver and registry used to look up the settlement component argument.
        public SetGoldCoopCommand(ICommandObjectResolver resolver, IObjectManager objectManager)
        {
            this.resolver = resolver;
            this.objectManager = objectManager;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This function can only be used by the server");


            if (int.TryParse(args[1], out int gold) == false)
            {
                return Failed("Unable to parse gold amount");
            }
            if (TryResolveSettlementComponent(objectManager, resolver, args[0], out var settlementComponent, out var componentError) == false)
                return Failed(componentError);

            settlementComponent.Gold = gold;

            return Succeeded($"Successfully set the SettlementComponent ({resolver.DisplayId(settlementComponent)}) Gold to '{args[1]}'");

        }
    }

    // coop.debug.settlementcomponent.set_is_owner_unassigned town_comp_ES3 true
    // Change Poros component IsOwnerUnassigned
    /// <summary>
    /// Changes <see cref="SettlementComponent.IsOwnerUnassigned"/>
    /// </summary>
    /// <param name="args"><see cref="SettlementComponent"/> id, new <see cref="SettlementComponent.IsOwnerUnassigned"/> value></param>
    /// <returns>info that is was successful</returns>
    public sealed class SetIsOwnerUnassignedCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlement_component";

        public string Name => "set_is_owner_unassigned";

        public string Description => "Sets is owner unassigned for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementComponentId", "Settlement component id, or the name or id of its settlement; quote names with spaces."),
            new ExpectedArgs("value", "The value."),
        };

        private readonly ICommandObjectResolver resolver;
        private readonly IObjectManager objectManager;

        // Injects the resolver and registry used to look up the settlement component argument.
        public SetIsOwnerUnassignedCoopCommand(ICommandObjectResolver resolver, IObjectManager objectManager)
        {
            this.resolver = resolver;
            this.objectManager = objectManager;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This function can only be used by the server");


            if (bool.TryParse(args[1], out bool value) == false)
            {
                return Failed("Unable to parse IsOwnerUnassigned");
            }
            if (TryResolveSettlementComponent(objectManager, resolver, args[0], out var settlementComponent, out var componentError) == false)
                return Failed(componentError);


            settlementComponent.IsOwnerUnassigned = value;


            return Succeeded($"Successfully set the SettlementComponent ({resolver.DisplayId(settlementComponent)}) IsOwnerUnassigned to '{args[1]}'");

        }
    }

    /// <summary>
    /// Set OwnerClan of a settlement from a hero name or id
    /// </summary>
    public sealed class SetOwnerClanCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "set_owner_clan";

        public string Description => "Sets owner clan for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementNameOrId", "Settlement name or id; quote names with spaces."),
            new ExpectedArgs("heroId", "Hero name or id; quote names with spaces."),
            new ExpectedArgs("transfer", "gift (default) or barter.", false),
        };

        private readonly ICommandObjectResolver resolver;

        // Injects the resolver that turns command arguments into game objects.
        public SetOwnerClanCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");
            var transfer = strings.Count > 2 ? strings[2] : "gift";
            if (transfer != "gift" && transfer != "barter")
                return Failed("Transfer must be gift or barter.");

            if (resolver.TryResolveSettlement(strings[0], out var settlement, out var settlementError) == false)
                return Failed(settlementError);

            if (resolver.TryResolveHero(strings[1], out var hero, out var heroError) == false)
                return Failed(heroError);

            if (transfer == "barter") ChangeOwnerOfSettlementAction.ApplyByBarter(hero, settlement);
            else ChangeOwnerOfSettlementAction.ApplyByGift(settlement, hero);

            return Succeeded($"{settlement.Name} ({settlement.StringId}) transferred to {hero.Name} ({hero.StringId})." + Environment.NewLine);

        }
    }
}
