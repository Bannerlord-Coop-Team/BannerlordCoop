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
    [CommandLineArgumentFunction("list", "coop.debug.campaignoptions")]
    public static string ListOptionsCommand(List<string> strings)
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

        return stringBuilder.ToString();
    }

    [CommandLineArgumentFunction("AutoAllocateClanMemberPerks", "coop.debug.campaignoptions")]
    public static string AutoAllocateClanMemberPerksCommand(List<string> strings)
    {
        return HandleBooleanOptionCommand(
            strings,
            nameof(AutoAllocateClanMemberPerks),
            () => AutoAllocateClanMemberPerks,
            value => AutoAllocateClanMemberPerks = value);
    }

    [CommandLineArgumentFunction("PlayerTroopsReceivedDamage", "coop.debug.campaignoptions")]
    public static string PlayerTroopsReceivedDamageCommand(List<string> strings)
    {
        return HandleDifficultyOptionCommand(
            strings,
            nameof(PlayerTroopsReceivedDamage),
            () => PlayerTroopsReceivedDamage,
            value => PlayerTroopsReceivedDamage = value);
    }

    [CommandLineArgumentFunction("RecruitmentDifficulty", "coop.debug.campaignoptions")]
    public static string RecruitmentDifficultyCommand(List<string> strings)
    {
        return HandleDifficultyOptionCommand(
            strings,
            nameof(RecruitmentDifficulty),
            () => RecruitmentDifficulty,
            value => RecruitmentDifficulty = value);
    }

    [CommandLineArgumentFunction("PlayerMapMovementSpeed", "coop.debug.campaignoptions")]
    public static string PlayerMapMovementSpeedCommand(List<string> strings)
    {
        return HandleDifficultyOptionCommand(
            strings,
            nameof(PlayerMapMovementSpeed),
            () => PlayerMapMovementSpeed,
            value => PlayerMapMovementSpeed = value);
    }

    [CommandLineArgumentFunction("StealthAndDisguiseDifficulty", "coop.debug.campaignoptions")]
    public static string StealthAndDisguiseDifficultyCommand(List<string> strings)
    {
        return HandleDifficultyOptionCommand(
            strings,
            nameof(StealthAndDisguiseDifficulty),
            () => StealthAndDisguiseDifficulty,
            value => StealthAndDisguiseDifficulty = value);
    }

    [CommandLineArgumentFunction("CombatAIDifficulty", "coop.debug.campaignoptions")]
    public static string CombatAIDifficultyCommand(List<string> strings)
    {
        return HandleDifficultyOptionCommand(
            strings,
            nameof(CombatAIDifficulty),
            () => CombatAIDifficulty,
            value => CombatAIDifficulty = value);
    }

    [CommandLineArgumentFunction("IsLifeDeathCycleDisabled", "coop.debug.campaignoptions")]
    public static string IsLifeDeathCycleDisabledCommand(List<string> strings)
    {
        return HandleBooleanOptionCommand(
            strings,
            nameof(IsLifeDeathCycleDisabled),
            () => IsLifeDeathCycleDisabled,
            value => IsLifeDeathCycleDisabled = value);
    }

    [CommandLineArgumentFunction("PersuasionSuccessChance", "coop.debug.campaignoptions")]
    public static string PersuasionSuccessChanceCommand(List<string> strings)
    {
        return HandleDifficultyOptionCommand(
            strings,
            nameof(PersuasionSuccessChance),
            () => PersuasionSuccessChance,
            value => PersuasionSuccessChance = value);
    }

    [CommandLineArgumentFunction("ClanMemberDeathChance", "coop.debug.campaignoptions")]
    public static string ClanMemberDeathChanceCommand(List<string> strings)
    {
        return HandleDifficultyOptionCommand(
            strings,
            nameof(ClanMemberDeathChance),
            () => ClanMemberDeathChance,
            value => ClanMemberDeathChance = value);
    }

    [CommandLineArgumentFunction("IsIronmanMode", "coop.debug.campaignoptions")]
    public static string IsIronmanModeCommand(List<string> strings)
    {
        if (strings.Count == 0)
            return $"{nameof(IsIronmanMode)} is {(IsIronmanMode ? "enabled" : "disabled")}.";

        if (ModInformation.IsClient)
            return "Managing campaign options is disabled on clients; the host does this.";

        return "This option can only be set at game start.";
    }

    [CommandLineArgumentFunction("BattleDeath", "coop.debug.campaignoptions")]
    public static string BattleDeathCommand(List<string> strings)
    {
        return HandleDifficultyOptionCommand(
            strings,
            nameof(BattleDeath),
            () => BattleDeath,
            value => BattleDeath = value);
    }

    [CommandLineArgumentFunction("PlayerReceivedDamageDifficulty", "coop.debug.campaignoptions")]
    public static string SetPlayerReceivedDamageDifficulty(List<string> strings)
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
            // Player received damage is not a campaign option. Need to update for clients separately
            UpdateOtherOptions();
        }

        return result;
    }

    private static string HandleBooleanOptionCommand(List<string> strings, string optionName, Func<bool> getter, Action<bool> setter)
    {
        if (strings.Count == 0)
            return $"{optionName} is currently {(getter() ? "enabled" : "disabled")}.";

        if (ModInformation.IsClient)
            return "Managing campaign options is disabled on clients; the host does this.";

        if (strings.Count != 1)
            return $"Usage: coop.debug.campaignoptions.{optionName} <true|false|1|0>";

        if (!TryParseBool(strings[0], out var value))
            return $"Invalid value '{strings[0]}'. Valid values: true, false, 1, 0.";

        setter(value);
        UpdateCampaignOptions();

        return $"{optionName} {(value ? "enabled" : "disabled")}.";
    }

    private static string HandleDifficultyOptionCommand(List<string> strings, string optionName, Func<Difficulty> getter, Action<Difficulty> setter)
    {
        if (strings.Count == 0)
            return $"{optionName} is currently {getter()}.";

        if (ModInformation.IsClient)
            return "Managing campaign options is disabled on clients; the host does this.";

        if (strings.Count != 1)
            return $"Usage: coop.debug.campaignoptions.{optionName} <veryeasy|easy|realistic|0|1|2>";

        if (!TryParseDifficulty(strings[0], out var value))
            return $"Invalid value '{strings[0]}'. Valid values: veryeasy, easy, realistic, 0, 1, 2.";

        setter(value);
        UpdateCampaignOptions();

        return $"{optionName} set to {value}.";
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
