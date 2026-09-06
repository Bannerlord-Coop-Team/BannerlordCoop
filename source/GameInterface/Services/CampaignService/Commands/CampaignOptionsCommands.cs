using Common.Commands;
using Common;
using Common.Messaging;
using GameInterface.Services.CampaignService.Messages;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using static TaleWorlds.CampaignSystem.CampaignOptions;
using static TaleWorlds.Library.CommandLineFunctionality;
using static TaleWorlds.MountAndBlade.BannerlordConfig;

namespace GameInterface.Services.CampaignService.Commands;

/// <summary>
/// Allows changing campaign options on the server through the console.
/// Split into multiple commands so users can see available options with auto-fill.
/// </summary>
internal class CampaignOptionsCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public sealed class CampaignOptionsListCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.campaign_options";

        public string Name => "list";

        public string Description => "Reports list.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            StringBuilder stringBuilder = new();

            stringBuilder.AppendLine($"PlayerReceivedDamageDifficulty: {(Difficulty)PlayerReceivedDamageDifficulty}");

            // Different order to match vanilla campaign options menu
            stringBuilder.AppendLine($"PlayerTroopsReceivedDamage: {PlayerTroopsReceivedDamage}");
            stringBuilder.AppendLine($"RecruitmentDifficulty: {RecruitmentDifficulty}");
            stringBuilder.AppendLine($"PlayerMapMovementSpeed: {PlayerMapMovementSpeed}");
            stringBuilder.AppendLine($"PersuasionSuccessChance: {PersuasionSuccessChance}");
            stringBuilder.AppendLine($"CombatAIDifficulty: {CombatAIDifficulty}");
            stringBuilder.AppendLine($"ClanMemberDeathChance: {ClanMemberDeathChance}");
            stringBuilder.AppendLine($"BattleDeath: {BattleDeath}");
            stringBuilder.AppendLine($"StealthAndDisguiseDifficulty: {StealthAndDisguiseDifficulty}");
            stringBuilder.AppendLine($"AutoAllocateClanMemberPerks: {AutoAllocateClanMemberPerks}");
            stringBuilder.AppendLine($"IsIronmanMode: {IsIronmanMode}");

            return Succeeded(stringBuilder.ToString());
        }
    }

    public sealed class CampaignOptionsAutoAllocateClanMemberPerksCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.campaign_options";

        public string Name => "auto_allocate_clan_member_perks";

        public string Description => "Runs the auto allocate clan member perks debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("value", "The option value.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            return HandleBooleanOptionCommand(
                strings,
                nameof(AutoAllocateClanMemberPerks),
                () => AutoAllocateClanMemberPerks,
                value => AutoAllocateClanMemberPerks = value);
        }
    }

    public sealed class CampaignOptionsPlayerTroopsReceivedDamageCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.campaign_options";

        public string Name => "player_troops_received_damage";

        public string Description => "Runs the player troops received damage debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("value", "The option value.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            return HandleDifficultyOptionCommand(
                strings,
                nameof(PlayerTroopsReceivedDamage),
                () => PlayerTroopsReceivedDamage,
                value => PlayerTroopsReceivedDamage = value);
        }
    }

    public sealed class CampaignOptionsRecruitmentDifficultyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.campaign_options";

        public string Name => "recruitment_difficulty";

        public string Description => "Runs the recruitment difficulty debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("value", "The option value.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            return HandleDifficultyOptionCommand(
                strings,
                nameof(RecruitmentDifficulty),
                () => RecruitmentDifficulty,
                value => RecruitmentDifficulty = value);
        }
    }

    public sealed class CampaignOptionsPlayerMapMovementSpeedCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.campaign_options";

        public string Name => "player_map_movement_speed";

        public string Description => "Runs the player map movement speed debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("value", "The option value.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            return HandleDifficultyOptionCommand(
                strings,
                nameof(PlayerMapMovementSpeed),
                () => PlayerMapMovementSpeed,
                value => PlayerMapMovementSpeed = value);
        }
    }

    public sealed class CampaignOptionsStealthAndDisguiseDifficultyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.campaign_options";

        public string Name => "stealth_and_disguise_difficulty";

        public string Description => "Runs the stealth and disguise difficulty debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("value", "The option value.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            return HandleDifficultyOptionCommand(
                strings,
                nameof(StealthAndDisguiseDifficulty),
                () => StealthAndDisguiseDifficulty,
                value => StealthAndDisguiseDifficulty = value);
        }
    }

    public sealed class CampaignOptionsCombatAiDifficultyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.campaign_options";

        public string Name => "combat_ai_difficulty";

        public string Description => "Runs the combat ai difficulty debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("value", "The option value.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            return HandleDifficultyOptionCommand(
                strings,
                nameof(CombatAIDifficulty),
                () => CombatAIDifficulty,
                value => CombatAIDifficulty = value);
        }
    }

    public sealed class CampaignOptionsIsLifeDeathCycleDisabledCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.campaign_options";

        public string Name => "is_life_death_cycle_disabled";

        public string Description => "Runs the is life death cycle disabled debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("value", "The option value.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            return HandleBooleanOptionCommand(
                strings,
                nameof(IsLifeDeathCycleDisabled),
                () => IsLifeDeathCycleDisabled,
                value => IsLifeDeathCycleDisabled = value);
        }
    }

    public sealed class CampaignOptionsPersuasionSuccessChanceCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.campaign_options";

        public string Name => "persuasion_success_chance";

        public string Description => "Runs the persuasion success chance debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("value", "The option value.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            return HandleDifficultyOptionCommand(
                strings,
                nameof(PersuasionSuccessChance),
                () => PersuasionSuccessChance,
                value => PersuasionSuccessChance = value);
        }
    }

    public sealed class CampaignOptionsClanMemberDeathChanceCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.campaign_options";

        public string Name => "clan_member_death_chance";

        public string Description => "Runs the clan member death chance debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("value", "The option value.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            return HandleDifficultyOptionCommand(
                strings,
                nameof(ClanMemberDeathChance),
                () => ClanMemberDeathChance,
                value => ClanMemberDeathChance = value);
        }
    }

    public sealed class CampaignOptionsIsIronmanModeCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.campaign_options";

        public string Name => "is_ironman_mode";

        public string Description => "Runs the is ironman mode debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("value", "The option value.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (strings.Count == 0)
                return Succeeded($"{nameof(IsIronmanMode)} is {(IsIronmanMode ? "enabled" : "disabled")}.");

            if (ModInformation.IsClient)
                return Failed("Managing campaign options is disabled on clients; the host does this.");

            return Failed("This option can only be set at game start.");
        }
    }

    public sealed class CampaignOptionsBattleDeathCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.campaign_options";

        public string Name => "battle_death";

        public string Description => "Runs the battle death debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("value", "The option value.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            return HandleDifficultyOptionCommand(
                strings,
                nameof(BattleDeath),
                () => BattleDeath,
                value => BattleDeath = value);
        }
    }

    public sealed class CampaignOptionsPlayerReceivedDamageDifficultyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.campaign_options";

        public string Name => "player_received_damage_difficulty";

        public string Description => "Runs the player received damage difficulty debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("value", "The option value.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            Difficulty oldValue = (Difficulty)PlayerReceivedDamageDifficulty;

            var result = HandleDifficultyOptionCommand(
                strings,
                nameof(PlayerReceivedDamageDifficulty),
                () => (Difficulty)PlayerReceivedDamageDifficulty,
                value => PlayerReceivedDamageDifficulty = (int)value);

            // Player received damage is not a campaign option. Need to update for clients separately
            if (oldValue != (Difficulty)PlayerReceivedDamageDifficulty)
            {
                UpdateOtherOptions();
            }

            return result;
        }
    }

    private static CoopCommandResult HandleBooleanOptionCommand(IReadOnlyList<string> strings, string optionName, Func<bool> getter, Action<bool> setter)
    {
        if (strings.Count == 0)
            return Succeeded($"{optionName} is currently {(getter() ? "enabled" : "disabled")}.");

        if (ModInformation.IsClient)
            return Failed("Managing campaign options is disabled on clients; the host does this.");

        if (!TryParseBool(strings[0], out var value))
            return Failed($"Invalid value '{strings[0]}'. Valid values: true, false, 1, 0.");

        setter(value);
        UpdateCampaignOptions();

        return Succeeded($"{optionName} {(value ? "enabled" : "disabled")}.");
    }

    private static CoopCommandResult HandleDifficultyOptionCommand(IReadOnlyList<string> strings, string optionName, Func<Difficulty> getter, Action<Difficulty> setter)
    {
        if (strings.Count == 0)
            return Succeeded($"{optionName} is currently {getter()}.");

        if (ModInformation.IsClient)
            return Failed("Managing campaign options is disabled on clients; the host does this.");

        if (!TryParseDifficulty(strings[0], out var value))
            return Failed($"Invalid value '{strings[0]}'. Valid values: veryeasy, easy, realistic, 0, 1, 2.");

        setter(value);
        UpdateCampaignOptions();

        return Succeeded($"{optionName} set to {value}.");
    }

    private static bool TryParseBool(string input, out bool value)
    {
        switch (input.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
            case "on":
            case "yes":
                value = true;
                return true;

            case "0":
            case "false":
            case "off":
            case "no":
                value = false;
                return true;

            default:
                value = default;
                return false;
        }
    }

    private static bool TryParseDifficulty(string input, out Difficulty value)
    {
        input = input.Trim();

        // Numeric values
        if (short.TryParse(input, out short numeric) &&
            Enum.IsDefined(typeof(Difficulty), numeric))
        {
            value = (Difficulty)numeric;
            return true;
        }

        // Enum values
        if (Enum.TryParse(input, ignoreCase: true, out Difficulty parsed) &&
            Enum.IsDefined(typeof(Difficulty), parsed))
        {
            value = parsed;
            return true;
        }

        value = default;
        return false;
    }

    private static void UpdateCampaignOptions()
    {
        MessageBroker.Instance.Publish(null, new UpdateCampaignOptions());
    }

    private static void UpdateOtherOptions()
    {
        MessageBroker.Instance.Publish(null, new UpdateOtherOptions());
    }
}
