using Common.Commands;
using Common;
using Common.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.Army;

namespace GameInterface.Services.Armies.Commands;

/// <summary>
/// Commands for <see cref="Army"/>
/// </summary>
public class ArmyDebugCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    // coop.debug.army.list
    /// <summary>
    /// Lists all the current Army
    /// </summary>

    public sealed class ArmyListCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.army";

        public string Name => "list";

        public string Description => "Lists registered armies.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            StringBuilder stringBuilder = new StringBuilder();

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to resolve {nameof(ArmyRegistry)}");
            }

            foreach (var army in Kingdom.All.SelectMany(kingdom => kingdom.Armies))
            {
                if (!objectManager.TryGetId(army, out var armyId))
                {
                    stringBuilder.AppendLine($"Unable to get id for Army Name: '{army.Name}'");
                    continue;
                }

                stringBuilder.AppendLine($"Name: '{army.Name}'");
                stringBuilder.AppendLine($"StringId: '{armyId}'");
            }

            return Succeeded(stringBuilder.ToString());
        }
    }

    // coop.debug.army.create empire town_EN2 lord_1_1 Raider
    // coop.debug.army.mobile_party_add Army_Created_1 lord_1_3_party_1
    // coop.debug.army.destroy Army_Created_1 NotEnoughParty
    // coop.debug.army.mobile_party_remove Army_Created_1 lord_1_3_party_1
    /// <summary>
    /// Creates a new army on the server and clients
    /// </summary>

    public sealed class ArmyCreateCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;

        public ArmyCreateCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.army";

        public string Name => "create";

        public string Description => "Creates an army on the server.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("kingdom_id", "Kingdom name or id; quote names with spaces."),
            new ExpectedArgs("target_settlement_id", "Target settlement name or id; quote names with spaces."),
            new ExpectedArgs("hero_leader_id", "Leader hero name or id; quote names with spaces."),
            new ExpectedArgs("army_type", "The ArmyTypes name or value."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var sb = new StringBuilder();
            if (ModInformation.IsClient)
            {
                return Failed("Command is only available to run on the server");
            }

            if (resolver.TryResolveKingdom(args[0], out var kingdom, out var error) == false)
            {
                return Failed(error);
            }

            if (resolver.TryResolveSettlement(args[1], out var targetSettlment, out error) == false)
            {
                return Failed(error);
            }

            if (resolver.TryResolveHero(args[2], out var armyLeader, out error) == false)
            {
                return Failed(error);
            }

            var armyTypeInt = args[3];
            if (Enum.TryParse(armyTypeInt, true, out ArmyTypes armyType) == false)
            {
                return Failed($"Unable to cast {armyTypeInt} to {nameof(ArmyTypes)}\n" +
                    GetArmyTypesUsage());
            }

            kingdom.CreateArmy(armyLeader, targetSettlment, armyType);
            var army = armyLeader.PartyBelongedTo?.Army;
            sb.AppendLine($"Created army {army.Name.ToString()}");
            return Succeeded(sb.ToString());
        }
    }

    private static string GetArmyTypesUsage(StringBuilder stringBuilder = null)
    {
        stringBuilder = stringBuilder ?? new StringBuilder();

        stringBuilder.Append($"\tArmy.ArmyTypes = [");

        foreach (var armyTypeEnum in Enum.GetNames(typeof(ArmyTypes)).Zip(Enum.GetValues(typeof(ArmyTypes)).Cast<int>()))
        {
            stringBuilder.AppendLine($"\t\t{armyTypeEnum.Item1} = {armyTypeEnum.Item2}");
        }

        stringBuilder.Append("\t]");

        return stringBuilder.ToString();
    }

    // coop.debug.army.destroy Army_Created_1 NotEnoughParty
    /// <summary>
    /// Deletes an army on the server and clients
    /// </summary>

    public sealed class ArmyDestroyCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;

        public ArmyDestroyCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.army";

        public string Name => "destroy";

        public string Description => "Destroys an army on the server.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("army_id", "Army name or id; quote names with spaces."),
            new ExpectedArgs("disband_reason", "The ArmyDispersionReason name or value."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Command is only available to run on the server");
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (resolver.TryResolveArmy(args[0], out var army, out var error) == false)
            {
                return Failed(error);
            }

            // Read before the disband unregisters the army.
            objectManager.TryGetId(army, out var armyId);

            var disbandArmyReason = args[1];
            if (Enum.TryParse(disbandArmyReason, true, out ArmyDispersionReason reason) == false)
            {
                return Failed($"Unable to cast {disbandArmyReason} to {nameof(ArmyDispersionReason)}\n" +
                    GetArmyDispersionReasonUsage());
            }
            var armyName = army.Name.ToString();
            DisbandArmyAction.ApplyInternal(army, reason);

            return Succeeded($"Destroyed army {armyName} with id {armyId}");
        }
    }

    private static string GetArmyDispersionReasonUsage(StringBuilder stringBuilder = null)
    {
        stringBuilder = stringBuilder ?? new StringBuilder();

        stringBuilder.Append($"\t{nameof(ArmyDispersionReason)} = [");

        foreach (var armyTypeEnum in Enum.GetNames(typeof(ArmyDispersionReason)).Zip(Enum.GetValues(typeof(ArmyDispersionReason)).Cast<int>()))
        {
            stringBuilder.AppendLine($"\t\t{armyTypeEnum.Item1} = {armyTypeEnum.Item2}");
        }

        stringBuilder.Append("\t]");

        return stringBuilder.ToString();
    }

    // coop.debug.army.mobile_party_list Army_Created_1
    /// <summary>
    /// Lists all the current Mobile Parties for an Army
    /// </summary>
    ///

    public sealed class ArmyMobilePartyListCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;

        public ArmyMobilePartyListCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.army";

        public string Name => "mobile_party_list";

        public string Description => "Lists parties in an army.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("army_id", "Army name or id; quote names with spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var stringBuilder = new StringBuilder();

            if (resolver.TryResolveArmy(args[0], out var army, out var error) == false)
            {
                return Failed(error);
            }

            foreach (var mobileParty in army.Parties)
            {
                stringBuilder.AppendLine($"Name: {mobileParty.Name}\nStringId: {mobileParty.StringId}");
            }

            return Succeeded(stringBuilder.ToString());
        }
    }

    // coop.debug.army.mobile_party_add Army_Created_1 lord_1_34_party_1
    /// <summary>
    /// Add a Mobile Party to an Army
    /// </summary>
    ///

    public sealed class ArmyMobilePartyAddCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;

        public ArmyMobilePartyAddCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.army";

        public string Name => "mobile_party_add";

        public string Description => "Adds a mobile party to an army.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("army_id", "Army name or id; quote names with spaces."),
            new ExpectedArgs("mobile_party_id", "Mobile party name or id; quote names with spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var stringBuilder = new StringBuilder();

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (resolver.TryResolveMobileParty(args[1], out var mobileParty, out var error) == false)
            {
                return Failed(error);
            }

            if (resolver.TryResolveArmy(args[0], out var army, out error) == false)
            {
                return Failed(error);
            }

            objectManager.TryGetId(army, out var armyId);

            mobileParty.Army = army;

            stringBuilder.AppendLine($"Added {mobileParty.Name} to {armyId}");

            return Succeeded(stringBuilder.ToString());
        }
    }

    // coop.debug.army.mobile_party_remove Army_Created_1 lord_1_3_party_1
    /// <summary>
    /// Add a Mobile Party to an Army
    /// </summary>
    ///

    public sealed class ArmyMobilePartyRemoveCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;

        public ArmyMobilePartyRemoveCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.army";

        public string Name => "mobile_party_remove";

        public string Description => "Removes a mobile party from an army.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("army_id", "Army name or id; quote names with spaces."),
            new ExpectedArgs("mobile_party_id", "Mobile party name or id; quote names with spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var stringBuilder = new StringBuilder();

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (resolver.TryResolveMobileParty(args[1], out var mobileParty, out var error) == false)
            {
                return Failed(error);
            }

            if (resolver.TryResolveArmy(args[0], out var army, out error) == false)
            {
                return Failed(error);
            }

            objectManager.TryGetId(army, out var armyId);

            mobileParty.Army = null;

            stringBuilder.AppendLine($"Removed {mobileParty.Name} from {armyId}");

            return Succeeded(stringBuilder.ToString());
        }
    }
    public sealed class ArmySetObjectiveCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;

        public ArmySetObjectiveCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.army";
        public string Name => "set_objective";
        public string Description => "Sets an army objective, or finishes it with None.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("army_id", "Army name or id; quote names with spaces."),
            new ExpectedArgs("type", "Settlement, MobileParty, or None."),
            new ExpectedArgs("object_id", "Objective settlement or mobile party name or id (quote names with spaces), or none when finishing."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Command is only available on the server");
            if (!ContainerProvider.TryResolve<IObjectManager>(out var manager)) return Failed("Unable to get ObjectManager");
            if (!resolver.TryResolveArmy(args[0], out var army, out var error)) return Failed(error);

            manager.TryGetId(army, out var armyId);
            var objectiveId = args.Count > 2 ? args[2] : "none";

            if (args[1] == "None")
                army.FinishArmyObjective();
            else if (args[1] == "Settlement")
            {
                if (!resolver.TryResolveSettlement(args[2], out var settlement, out error)) return Failed(error);
                army.AiBehaviorObject = settlement;
                manager.TryGetId(settlement, out objectiveId);
            }
            else if (args[1] == "MobileParty")
            {
                if (!resolver.TryResolveMobileParty(args[2], out var party, out error)) return Failed(error);
                army.AiBehaviorObject = party;
                manager.TryGetId(party, out objectiveId);
            }
            else return Failed("Objective type must be Settlement, MobileParty, or None");

            return Succeeded($"Army {armyId} objective: {args[1]} {objectiveId}");
        }
    }

    // coop.debug.army.info Army_Created_1
    /// <summary>
    /// Info about army
    /// </summary>
    ///

    public sealed class ArmyInfoCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;

        public ArmyInfoCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.army";

        public string Name => "info";

        public string Description => "Reports state for an army.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("army_id", "Army name or id; quote names with spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var sb = new StringBuilder();
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }
            if (resolver.TryResolveArmy(args[0], out var army, out var error) == false)
            {
                return Failed(error);
            }
            objectManager.TryGetId(army, out var armyId);
            sb.AppendLine($"AttachedParties count: {army?.LeaderParty.AttachedParties?.Count}");
            sb.AppendLine($"{army._parties.Count}");
            sb.AppendLine($"LeaderHero: {army?.LeaderParty?.LeaderHero?.Name}");
            sb.AppendLine($"Army.name {army.Name}");
            sb.AppendLine($"Armyowner {army.ArmyOwner.Name}");
            sb.AppendLine($"leaderparty owner {army?.LeaderParty.Owner.Name}");
            sb.AppendLine($"armycohesion: {army?.Cohesion}");
            string objectiveId = null;
            if (army.AiBehaviorObject != null && !objectManager.TryGetId(army.AiBehaviorObject, out objectiveId))
                return Failed("Army objective is not registered");
            sb.AppendLine($"ArmyId: {armyId}");
            sb.AppendLine($"ObjectiveType: {army.AiBehaviorObject?.GetType().Name ?? "None"}");
            sb.AppendLine($"ObjectiveId: {objectiveId ?? "none"}");
            sb.AppendLine($"Dispersing: {army._armyIsDispersing}");
            return Succeeded(sb.ToString());
        }
    }
    }
