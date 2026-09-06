using Common.Commands;
using Common;
using Common.Extensions;
using GameInterface.Services.ObjectManager;
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
        public string Prefix => "coop.debug.army";

        public string Name => "create";

        public string Description => "Creates an army on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
            new ExpectedArgs("target_settlement_id", "The registered target settlement id."),
            new ExpectedArgs("hero_leader_id", "The registered leader hero id."),
            new ExpectedArgs("army_type", "The ArmyTypes name or value."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var sb = new StringBuilder();
            if (ModInformation.IsClient)
            {
                return Failed("Command is only available to run on the server");
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed("Unable to get ObjectManager");
            }

            var kingdomId = args[0];
            if (objectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom) == false)
            {
                return Failed($"Unable to get Kingdom with {kingdomId}");
            }

            var targetSettlmentId = args[1];
            if (objectManager.TryGetObject<Settlement>(targetSettlmentId, out var targetSettlment) == false)
            {
                return Failed($"Unable to get Settlement with {targetSettlmentId}");
            }

            var heroLeaderId = args[2];
            if (objectManager.TryGetObject<Hero>(heroLeaderId, out var armyLeader) == false)
            {
                return Failed($"Unable to get Hero with {heroLeaderId}");
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
        public string Prefix => "coop.debug.army";

        public string Name => "destroy";

        public string Description => "Destroys an army on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("army_id", "The registered army id."),
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

            var armyId = args[0];
            if (objectManager.TryGetObject<Army>(armyId, out var army) == false)
            {
                return Failed($"Unable to get {nameof(Army)} with {armyId}");
            }

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
        public string Prefix => "coop.debug.army";

        public string Name => "mobile_party_list";

        public string Description => "Lists parties in an army.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("army_id", "The registered army id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var stringBuilder = new StringBuilder();

            string armyId = args[0];

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (objectManager.TryGetObject<Army>(armyId, out var army) == false)
            {
                return Failed($"Unable to get {nameof(Army)} with {armyId}");
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
        public string Prefix => "coop.debug.army";

        public string Name => "mobile_party_add";

        public string Description => "Adds a mobile party to an army.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("army_id", "The registered army id."),
            new ExpectedArgs("mobile_party_id", "The registered mobile party id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var stringBuilder = new StringBuilder();

            string armyId = args[0];
            string mobilePartyId = args[1];

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (objectManager.TryGetObject(mobilePartyId, out MobileParty mobileParty) == false)
            {
                return Failed($"Unable to get {nameof(MobileParty)} with {mobilePartyId}");
            }

            if (objectManager.TryGetObject<Army>(armyId, out var army) == false)
            {
                return Failed($"Unable to get {nameof(Army)} with {armyId}");
            }

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
        public string Prefix => "coop.debug.army";

        public string Name => "mobile_party_remove";

        public string Description => "Removes a mobile party from an army.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("army_id", "The registered army id."),
            new ExpectedArgs("mobile_party_id", "The registered mobile party id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var stringBuilder = new StringBuilder();

            string armyId = args[0];
            string mobilePartyId = args[1];

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (objectManager.TryGetObject(mobilePartyId, out MobileParty mobileParty) == false)
            {
                return Failed($"Unable to get {nameof(MobileParty)} with {mobilePartyId}");
            }

            if (objectManager.TryGetObject<Army>(armyId, out var army) == false)
            {
                return Failed($"Unable to get {nameof(Army)} with {armyId}");
            }

            mobileParty.Army = null;

            stringBuilder.AppendLine($"Removed {mobileParty.Name} from {armyId}");

            return Succeeded(stringBuilder.ToString());
        }
    }
    // coop.debug.army.info Army_Created_1
    /// <summary>
    /// Info about army
    /// </summary>
    ///

    public sealed class ArmyInfoCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.army";

        public string Name => "info";

        public string Description => "Reports state for an army.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("army_id", "The registered army id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var sb = new StringBuilder();
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }
            if (objectManager.TryGetObject<Army>(args[0], out var army) == false)
            {
                return Failed($"Unable to get {nameof(Army)} with {args[0]}");
            }
            sb.AppendLine($"AttachedParties count: {army?.LeaderParty.AttachedParties?.Count}");
            sb.AppendLine($"{army._parties.Count}");
            sb.AppendLine($"LeaderHero: {army?.LeaderParty?.LeaderHero?.Name}");
            sb.AppendLine($"Army.name {army.Name}");
            sb.AppendLine($"Armyowner {army.ArmyOwner.Name}");
            sb.AppendLine($"leaderparty owner {army?.LeaderParty.Owner.Name}");
            sb.AppendLine($"armycohesion: {army?.Cohesion}");
            return Succeeded(sb.ToString());
        }
    }
    }
