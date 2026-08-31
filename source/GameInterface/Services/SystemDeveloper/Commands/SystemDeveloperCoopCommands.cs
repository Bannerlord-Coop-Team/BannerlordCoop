using Common.Commands;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.SystemDeveloper.Commands;

internal static class SystemDeveloperLegacyCommandResult
{
    public static CoopCommandResult FromOutput(
        string output,
        params string[] commandFailurePrefixes)
    {
        if (output == null) return new CoopCommandResult(false, "Command returned no output.", "command_failed");

        bool succeeded = !LooksLikeFailure(output, commandFailurePrefixes);
        return new CoopCommandResult(succeeded, output, succeeded ? null : "command_rejected");
    }

    private static bool LooksLikeFailure(
        string output,
        string[] commandFailurePrefixes)
    {
        // Backing commands preserve string APIs, so every scoped rejection prefix is listed here.
        string[] failurePrefixes =
        {
            "Usage:",
            "Invalid ",
            "Unable ",
            "Error ",
            "Failed",
            "No ",
            "Run ",
            "Command can ",
            "Command is ",
            "The command ",
            "The '",
            "This command ",
            "This function ",
            "Could not ",
            "Cannot ",
            "Managing campaign options ",
            "An integer amount ",
            "Close the current prompt ",
            "Campaign map camera is unavailable",
            "Campaign map screen is unavailable",
            "Seconds must ",
            "Leave the active ",
            "Active state is already ",
            "Saving overlay: UNAVAILABLE",
            "Cheats are currently disabled ",
            "Hero name argument required",
            "Hero not found",
            "Hero '",
            "Item object not found",
            "Town name argument required",
            "Town not found",
            "Unknown ",
            "Quest type ",
            "A client AI-lord ",
            "An AI-lord ",
            "A visual test fixture ",
            "A save is already ",
            "Captor ",
            "PartyScreenLogic ",
            "The active ",
            "The evidence hold ",
            "Autosaves are disabled",
            "Not advertising",
            "Tactical unit symbols configuration is unavailable",
        };

        foreach (string prefix in failurePrefixes)
        {
            if (output.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }
        foreach (string prefix in commandFailurePrefixes)
        {
            if (output.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return output.IndexOf(" not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" was not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" can only be ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" must be run ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" failed.", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" cannot ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" did not ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" does not ", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

public interface ICampaignOptionsListCommand : ICoopCommand
{
}

public sealed class CampaignOptionsListCommand : ICampaignOptionsListCommand
{
    public string Prefix => "coop.debug.campaign_options";

    public string Name => "list";

    public string Description => "Reports list.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CampaignService.Commands.CampaignOptionsCommands.ListOptionsCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICampaignOptionsAutoAllocateClanMemberPerksCommand : ICoopCommand
{
}

public sealed class CampaignOptionsAutoAllocateClanMemberPerksCommand : ICampaignOptionsAutoAllocateClanMemberPerksCommand
{
    public string Prefix => "coop.debug.campaign_options";

    public string Name => "auto_allocate_clan_member_perks";

    public string Description => "Runs the auto allocate clan member perks debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("value", "The option value.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CampaignService.Commands.CampaignOptionsCommands.AutoAllocateClanMemberPerksCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICampaignOptionsPlayerTroopsReceivedDamageCommand : ICoopCommand
{
}

public sealed class CampaignOptionsPlayerTroopsReceivedDamageCommand : ICampaignOptionsPlayerTroopsReceivedDamageCommand
{
    public string Prefix => "coop.debug.campaign_options";

    public string Name => "player_troops_received_damage";

    public string Description => "Runs the player troops received damage debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("value", "The option value.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CampaignService.Commands.CampaignOptionsCommands.PlayerTroopsReceivedDamageCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICampaignOptionsRecruitmentDifficultyCommand : ICoopCommand
{
}

public sealed class CampaignOptionsRecruitmentDifficultyCommand : ICampaignOptionsRecruitmentDifficultyCommand
{
    public string Prefix => "coop.debug.campaign_options";

    public string Name => "recruitment_difficulty";

    public string Description => "Runs the recruitment difficulty debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("value", "The option value.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CampaignService.Commands.CampaignOptionsCommands.RecruitmentDifficultyCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICampaignOptionsPlayerMapMovementSpeedCommand : ICoopCommand
{
}

public sealed class CampaignOptionsPlayerMapMovementSpeedCommand : ICampaignOptionsPlayerMapMovementSpeedCommand
{
    public string Prefix => "coop.debug.campaign_options";

    public string Name => "player_map_movement_speed";

    public string Description => "Runs the player map movement speed debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("value", "The option value.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CampaignService.Commands.CampaignOptionsCommands.PlayerMapMovementSpeedCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICampaignOptionsStealthAndDisguiseDifficultyCommand : ICoopCommand
{
}

public sealed class CampaignOptionsStealthAndDisguiseDifficultyCommand : ICampaignOptionsStealthAndDisguiseDifficultyCommand
{
    public string Prefix => "coop.debug.campaign_options";

    public string Name => "stealth_and_disguise_difficulty";

    public string Description => "Runs the stealth and disguise difficulty debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("value", "The option value.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CampaignService.Commands.CampaignOptionsCommands.StealthAndDisguiseDifficultyCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICampaignOptionsCombatAiDifficultyCommand : ICoopCommand
{
}

public sealed class CampaignOptionsCombatAiDifficultyCommand : ICampaignOptionsCombatAiDifficultyCommand
{
    public string Prefix => "coop.debug.campaign_options";

    public string Name => "combat_ai_difficulty";

    public string Description => "Runs the combat ai difficulty debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("value", "The option value.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CampaignService.Commands.CampaignOptionsCommands.CombatAIDifficultyCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICampaignOptionsIsLifeDeathCycleDisabledCommand : ICoopCommand
{
}

public sealed class CampaignOptionsIsLifeDeathCycleDisabledCommand : ICampaignOptionsIsLifeDeathCycleDisabledCommand
{
    public string Prefix => "coop.debug.campaign_options";

    public string Name => "is_life_death_cycle_disabled";

    public string Description => "Runs the is life death cycle disabled debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("value", "The option value.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CampaignService.Commands.CampaignOptionsCommands.IsLifeDeathCycleDisabledCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICampaignOptionsPersuasionSuccessChanceCommand : ICoopCommand
{
}

public sealed class CampaignOptionsPersuasionSuccessChanceCommand : ICampaignOptionsPersuasionSuccessChanceCommand
{
    public string Prefix => "coop.debug.campaign_options";

    public string Name => "persuasion_success_chance";

    public string Description => "Runs the persuasion success chance debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("value", "The option value.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CampaignService.Commands.CampaignOptionsCommands.PersuasionSuccessChanceCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICampaignOptionsClanMemberDeathChanceCommand : ICoopCommand
{
}

public sealed class CampaignOptionsClanMemberDeathChanceCommand : ICampaignOptionsClanMemberDeathChanceCommand
{
    public string Prefix => "coop.debug.campaign_options";

    public string Name => "clan_member_death_chance";

    public string Description => "Runs the clan member death chance debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("value", "The option value.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CampaignService.Commands.CampaignOptionsCommands.ClanMemberDeathChanceCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICampaignOptionsIsIronmanModeCommand : ICoopCommand
{
}

public sealed class CampaignOptionsIsIronmanModeCommand : ICampaignOptionsIsIronmanModeCommand
{
    public string Prefix => "coop.debug.campaign_options";

    public string Name => "is_ironman_mode";

    public string Description => "Runs the is ironman mode debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("value", "The option value.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CampaignService.Commands.CampaignOptionsCommands.IsIronmanModeCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICampaignOptionsBattleDeathCommand : ICoopCommand
{
}

public sealed class CampaignOptionsBattleDeathCommand : ICampaignOptionsBattleDeathCommand
{
    public string Prefix => "coop.debug.campaign_options";

    public string Name => "battle_death";

    public string Description => "Runs the battle death debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("value", "The option value.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CampaignService.Commands.CampaignOptionsCommands.BattleDeathCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICampaignOptionsPlayerReceivedDamageDifficultyCommand : ICoopCommand
{
}

public sealed class CampaignOptionsPlayerReceivedDamageDifficultyCommand : ICampaignOptionsPlayerReceivedDamageDifficultyCommand
{
    public string Prefix => "coop.debug.campaign_options";

    public string Name => "player_received_damage_difficulty";

    public string Description => "Runs the player received damage difficulty debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("value", "The option value.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CampaignService.Commands.CampaignOptionsCommands.SetPlayerReceivedDamageDifficulty(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IModConfigListCommand : ICoopCommand
{
}

public sealed class ModConfigListCommand : IModConfigListCommand
{
    public string Prefix => "coop.debug.mod_config";

    public string Name => "list";

    public string Description => "Reports list.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CampaignService.Commands.ModOptionsCommands.ListOptionsCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICaravansViewProhibitedKingdomsCommand : ICoopCommand
{
}

public sealed class CaravansViewProhibitedKingdomsCommand : ICaravansViewProhibitedKingdomsCommand
{
    public string Prefix => "coop.debug.caravans";

    public string Name => "view_prohibited_kingdoms";

    public string Description => "Reports view prohibited kingdoms.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Caravans.Commands.CaravansCommands.ViewProhibitedKingdomsCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICaravansViewInteractedCaravansCommand : ICoopCommand
{
}

public sealed class CaravansViewInteractedCaravansCommand : ICaravansViewInteractedCaravansCommand
{
    public string Prefix => "coop.debug.caravans";

    public string Name => "view_interacted_caravans";

    public string Description => "Reports view interacted caravans.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Caravans.Commands.CaravansCommands.ViewInteractedCaravansCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICaravansViewTakenTradeRumorsCommand : ICoopCommand
{
}

public sealed class CaravansViewTakenTradeRumorsCommand : ICaravansViewTakenTradeRumorsCommand
{
    public string Prefix => "coop.debug.caravans";

    public string Name => "view_taken_trade_rumors";

    public string Description => "Reports view taken trade rumors.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Caravans.Commands.CaravansCommands.ViewTakenTradeRumorsCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICaravansViewTradeActionLogsCommand : ICoopCommand
{
}

public sealed class CaravansViewTradeActionLogsCommand : ICaravansViewTradeActionLogsCommand
{
    public string Prefix => "coop.debug.caravans";

    public string Name => "view_trade_action_logs";

    public string Description => "Reports view trade action logs.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Caravans.Commands.CaravansCommands.ViewTradeActionLogs(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICaravansViewLootedCaravansCommand : ICoopCommand
{
}

public sealed class CaravansViewLootedCaravansCommand : ICaravansViewLootedCaravansCommand
{
    public string Prefix => "coop.debug.caravans";

    public string Name => "view_looted_caravans";

    public string Description => "Reports view looted caravans.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Caravans.Commands.CaravansCommands.ViewLootedCaravans(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IHeroDeveloperStatsCommand : ICoopCommand
{
}

public sealed class HeroDeveloperStatsCommand : IHeroDeveloperStatsCommand
{
    public string Prefix => "coop.debug.hero_developer";

    public string Name => "stats";

    public string Description => "Reports stats.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name", "The exact hero display name.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CharacterDevelopers.Commands.CharacterDeveloperCommands.HeroStatsCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICharacterObjectsInfoCommand : ICoopCommand
{
}

public sealed class CharacterObjectsInfoCommand : ICharacterObjectsInfoCommand
{
    public string Prefix => "coop.debug.character_objects";

    public string Name => "info";

    public string Description => "Reports info.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("character_id", "The registered character id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CharacterObjects.Commands.CharacterObjectCommands.Info(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICharacterObjectsListCommand : ICoopCommand
{
}

public sealed class CharacterObjectsListCommand : ICharacterObjectsListCommand
{
    public string Prefix => "coop.debug.character_objects";

    public string Name => "list";

    public string Description => "Reports list.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.CharacterObjects.Commands.CharacterObjectCommands.ListCharacterObjects(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICoopBugReportLogSharingCommand : ICoopCommand
{
}

public sealed class CoopBugReportLogSharingCommand : ICoopBugReportLogSharingCommand
{
    public string Prefix => "coop";

    public string Name => "bug_report_log_sharing";

    public string Description => "Runs the bug report log sharing debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("mode", "status, enable, or disable.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.BugReportLogSharingCommand.Configure(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IFixCameraCommand : ICoopCommand
{
}

public sealed class FixCameraCommand : IFixCameraCommand
{
    public string Prefix => "coop.debug";

    public string Name => "fix_camera";

    public string Description => "Runs the fix camera debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.CameraReset.ChangeClanLeader(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IMapCameraFocusMainPartyCommand : ICoopCommand
{
}

public sealed class MapCameraFocusMainPartyCommand : IMapCameraFocusMainPartyCommand
{
    public string Prefix => "coop.debug.map_camera";

    public string Name => "focus_main_party";

    public string Description => "Runs the focus main party debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.CameraReset.FocusMainParty(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IMapCameraStateCommand : ICoopCommand
{
}

public sealed class MapCameraStateCommand : IMapCameraStateCommand
{
    public string Prefix => "coop.debug.map_camera";

    public string Name => "state";

    public string Description => "Reports state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.CameraReset.GetState(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IGameThreadInstrumentCommand : ICoopCommand
{
}

public sealed class GameThreadInstrumentCommand : IGameThreadInstrumentCommand
{
    public string Prefix => "coop.debug.game_thread";

    public string Name => "instrument";

    public string Description => "Runs the instrument debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("mode", "on, off, toggle, or status.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.GameThreadDebugCommand.Instrument(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IGameThreadStallCommand : ICoopCommand
{
}

public sealed class GameThreadStallCommand : IGameThreadStallCommand
{
    public string Prefix => "coop.debug.game_thread";

    public string Name => "stall";

    public string Description => "Runs the stall debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("milliseconds", "The stall duration from 1 through 5000 milliseconds.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.GameThreadDebugCommand.Stall(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IMetricsPartySyncPerformanceLogsCommand : ICoopCommand
{
}

public sealed class MetricsPartySyncPerformanceLogsCommand : IMetricsPartySyncPerformanceLogsCommand
{
    public string Prefix => "coop.debug.metrics";

    public string Name => "party_sync_performance_logs";

    public string Description => "Runs the party sync performance logs debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("mode", "on, off, or status.", isRequired: true),
        new ExpectedArgs("seconds", "The logging duration in seconds when enabling.", isRequired: false),
        new ExpectedArgs("file_name", "The output file name when enabling.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.PartySyncPerformanceLogsCommand.PartySyncPerformanceLogs(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IUiCloseScreenCommand : ICoopCommand
{
}

public sealed class UiCloseScreenCommand : IUiCloseScreenCommand
{
    public string Prefix => "coop.debug.ui";

    public string Name => "close_screen";

    public string Description => "Runs the close screen debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.UiDebugCommands.CloseScreen(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IUiPrepareEvidenceMapCommand : ICoopCommand
{
}

public sealed class UiPrepareEvidenceMapCommand : IUiPrepareEvidenceMapCommand
{
    public string Prefix => "coop.debug.ui";

    public string Name => "prepare_evidence_map";

    public string Description => "Runs the prepare evidence map debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.UiDebugCommands.PrepareEvidenceMap(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IUiEvidenceMapStateCommand : ICoopCommand
{
}

public sealed class UiEvidenceMapStateCommand : IUiEvidenceMapStateCommand
{
    public string Prefix => "coop.debug.ui";

    public string Name => "evidence_map_state";

    public string Description => "Reports evidence map state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.UiDebugCommands.EvidenceMapState(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IUiLeaveSettlementEncounterCommand : ICoopCommand
{
}

public sealed class UiLeaveSettlementEncounterCommand : IUiLeaveSettlementEncounterCommand
{
    public string Prefix => "coop.debug.ui";

    public string Name => "leave_settlement_encounter";

    public string Description => "Runs the leave settlement encounter debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.UiDebugCommands.LeaveSettlementEncounter(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

#if DEBUG
public interface IUiMapClickOffsetCommand : ICoopCommand
{
}

public sealed class UiMapClickOffsetCommand : IUiMapClickOffsetCommand
{
    public string Prefix => "coop.debug.ui";

    public string Name => "map_click_offset";

    public string Description => "Runs the map click offset debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("offset_x", "The horizontal map offset.", isRequired: true),
        new ExpectedArgs("offset_y", "The vertical map offset.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.UiDebugCommands.MapClickOffset(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IUiMapMovementStateCommand : ICoopCommand
{
}

public sealed class UiMapMovementStateCommand : IUiMapMovementStateCommand
{
    public string Prefix => "coop.debug.ui";

    public string Name => "map_movement_state";

    public string Description => "Reports map movement state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.UiDebugCommands.MapMovementState(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}
#endif

public interface IUiSwitchMenuCommand : ICoopCommand
{
}

public sealed class UiSwitchMenuCommand : IUiSwitchMenuCommand
{
    public string Prefix => "coop.debug.ui";

    public string Name => "switch_menu";

    public string Description => "Runs the switch menu debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("menu_id", "The game menu id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.UiDebugCommands.SwitchMenu(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IUiPopStateCommand : ICoopCommand
{
}

public sealed class UiPopStateCommand : IUiPopStateCommand
{
    public string Prefix => "coop.debug.ui";

    public string Name => "pop_state";

    public string Description => "Reports pop state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.UiDebugCommands.PopState(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IUiActiveStateCommand : ICoopCommand
{
}

public sealed class UiActiveStateCommand : IUiActiveStateCommand
{
    public string Prefix => "coop.debug.ui";

    public string Name => "active_state";

    public string Description => "Reports active state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.UiDebugCommands.ActiveState(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IUiLoadingWindowStateCommand : ICoopCommand
{
}

public sealed class UiLoadingWindowStateCommand : IUiLoadingWindowStateCommand
{
    public string Prefix => "coop.debug.ui";

    public string Name => "loading_window_state";

    public string Description => "Reports loading window state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.UiDebugCommands.LoadingWindowState(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IUiSavingOverlayStateCommand : ICoopCommand
{
}

public sealed class UiSavingOverlayStateCommand : IUiSavingOverlayStateCommand
{
    public string Prefix => "coop.debug.ui";

    public string Name => "saving_overlay_state";

    public string Description => "Reports saving overlay state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.UiDebugCommands.SavingOverlayState(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICoopUnstuckCommand : ICoopCommand
{
}

public sealed class CoopUnstuckCommand : ICoopUnstuckCommand
{
    public string Prefix => "coop";

    public string Name => "unstuck";

    public string Description => "Runs the unstuck debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.GameDebug.Commands.UnstuckCommand.Unstuck(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IHeroDeveloperAddSkillXpCommand : ICoopCommand
{
}

public sealed class HeroDeveloperAddSkillXpCommand : IHeroDeveloperAddSkillXpCommand
{
    public string Prefix => "coop.debug.hero_developer";

    public string Name => "add_skill_xp";

    public string Description => "Runs the add skill xp debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name_or_id", "The hero display name or StringId.", isRequired: true),
        new ExpectedArgs("skill_name", "The skill object name.", isRequired: true),
        new ExpectedArgs("xp_amount", "The amount of skill experience.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.HeroDevelopers.Commands.HeroDeveloperCommands.AddSkillXpCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IHeroDeveloperAddAttributePointsCommand : ICoopCommand
{
}

public sealed class HeroDeveloperAddAttributePointsCommand : IHeroDeveloperAddAttributePointsCommand
{
    public string Prefix => "coop.debug.hero_developer";

    public string Name => "add_attribute_points";

    public string Description => "Runs the add attribute points debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name_or_id", "The hero display name or StringId.", isRequired: true),
        new ExpectedArgs("point_count", "The number of attribute points.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.HeroDevelopers.Commands.HeroDeveloperCommands.AddAttributePointsCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IHeroDeveloperAddFocusPointsCommand : ICoopCommand
{
}

public sealed class HeroDeveloperAddFocusPointsCommand : IHeroDeveloperAddFocusPointsCommand
{
    public string Prefix => "coop.debug.hero_developer";

    public string Name => "add_focus_points";

    public string Description => "Runs the add focus points debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name_or_id", "The hero display name or StringId.", isRequired: true),
        new ExpectedArgs("point_count", "The number of focus points.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.HeroDevelopers.Commands.HeroDeveloperCommands.AddFocusPointsCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IHeroDeveloperResetSkillsCommand : ICoopCommand
{
}

public sealed class HeroDeveloperResetSkillsCommand : IHeroDeveloperResetSkillsCommand
{
    public string Prefix => "coop.debug.hero_developer";

    public string Name => "reset_skills";

    public string Description => "Runs the reset skills debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name_or_id", "The hero display name or StringId.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.HeroDevelopers.Commands.HeroDeveloperCommands.ResetSkillsCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IInventoryItemIdsCommand : ICoopCommand
{
}

public sealed class InventoryItemIdsCommand : IInventoryItemIdsCommand
{
    public string Prefix => "coop.debug.inventory";

    public string Name => "item_ids";

    public string Description => "Runs the item ids debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name", "The exact hero display name.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Inventory.Commands.InventoryCommands.ViewItemIdsCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IInventoryItemValuesCommand : ICoopCommand
{
}

public sealed class InventoryItemValuesCommand : IInventoryItemValuesCommand
{
    public string Prefix => "coop.debug.inventory";

    public string Name => "item_values";

    public string Description => "Runs the item values debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name", "The exact hero display name.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Inventory.Commands.InventoryCommands.ViewItemValuesCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IInventoryHeroEquipmentCommand : ICoopCommand
{
}

public sealed class InventoryHeroEquipmentCommand : IInventoryHeroEquipmentCommand
{
    public string Prefix => "coop.debug.inventory";

    public string Name => "hero_equipment";

    public string Description => "Runs the hero equipment debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name", "The exact hero display name.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Inventory.Commands.InventoryCommands.HeroEquipmentCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IInventoryGiveAnimalsCommand : ICoopCommand
{
}

public sealed class InventoryGiveAnimalsCommand : IInventoryGiveAnimalsCommand
{
    public string Prefix => "coop.debug.inventory";

    public string Name => "give_animals";

    public string Description => "Runs the give animals debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name", "The exact hero display name.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Inventory.Commands.InventoryCommands.GiveAnimalsCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IInventoryGiveWarhorsesCommand : ICoopCommand
{
}

public sealed class InventoryGiveWarhorsesCommand : IInventoryGiveWarhorsesCommand
{
    public string Prefix => "coop.debug.inventory";

    public string Name => "give_warhorses";

    public string Description => "Runs the give warhorses debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name", "The exact hero display name.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Inventory.Commands.InventoryCommands.GiveWarhorsesCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IInventoryViewPlayerTradeDataCommand : ICoopCommand
{
}

public sealed class InventoryViewPlayerTradeDataCommand : IInventoryViewPlayerTradeDataCommand
{
    public string Prefix => "coop.debug.inventory";

    public string Name => "view_player_trade_data";

    public string Description => "Reports view player trade data.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Inventory.TradeSkills.Commands.TradeSkillCommands.ViewPlayerTradeDataCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IInventoryViewPlayerTradeRumorsCommand : ICoopCommand
{
}

public sealed class InventoryViewPlayerTradeRumorsCommand : IInventoryViewPlayerTradeRumorsCommand
{
    public string Prefix => "coop.debug.inventory";

    public string Name => "view_player_trade_rumors";

    public string Description => "Reports view player trade rumors.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Inventory.TradeSkills.Commands.TradeSkillCommands.ViewPlayerTradeRumorsCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IInventoryViewEnteredSettlementsCommand : ICoopCommand
{
}

public sealed class InventoryViewEnteredSettlementsCommand : IInventoryViewEnteredSettlementsCommand
{
    public string Prefix => "coop.debug.inventory";

    public string Name => "view_entered_settlements";

    public string Description => "Reports view entered settlements.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Inventory.TradeSkills.Commands.TradeSkillCommands.ViewPlayerEnteredSettlementsCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IIssuesGiveCommand : ICoopCommand
{
}

public sealed class IssuesGiveCommand : IIssuesGiveCommand
{
    public string Prefix => "coop.debug.issues";

    public string Name => "give";

    public string Description => "Runs the give debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_id", "The registered hero id.", isRequired: true),
        new ExpectedArgs("quest_type_key", "The issue type key.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Issues.Commands.IssuesDebugCommand.Give(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IIssuesCompleteCommand : ICoopCommand
{
}

public sealed class IssuesCompleteCommand : IIssuesCompleteCommand
{
    public string Prefix => "coop.debug.issues";

    public string Name => "complete";

    public string Description => "Runs the complete debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_id", "The registered issue owner hero id.", isRequired: true),
        new ExpectedArgs("outcome", "success, cancel, fail, timeout, or betrayal.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Issues.Commands.IssuesDebugCommand.Complete(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IIssuesListTypesCommand : ICoopCommand
{
}

public sealed class IssuesListTypesCommand : IIssuesListTypesCommand
{
    public string Prefix => "coop.debug.issues";

    public string Name => "list_types";

    public string Description => "Reports list types.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Issues.Commands.IssuesDebugCommand.ListTypes(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IItemObjectDataCommand : ICoopCommand
{
}

public sealed class ItemObjectDataCommand : IItemObjectDataCommand
{
    public string Prefix => "coop.debug.item_object";

    public string Name => "data";

    public string Description => "Reports data.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("item_id", "The registered item object id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.ItemObjects.Commands.ItemObjectCommands.ViewCraftedItemData(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IItemRostersAddRandomItemCommand : ICoopCommand
{
}

public sealed class ItemRostersAddRandomItemCommand : IItemRostersAddRandomItemCommand
{
    public string Prefix => "coop.debug.item_rosters";

    public string Name => "add_random_item";

    public string Description => "Runs the add random item debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("settlement_id", "The settlement StringId.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.ItemRosters.Commands.ItemRosterDebugCommands.AddRandomItem(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IItemRostersAddItemBurstCommand : ICoopCommand
{
}

public sealed class ItemRostersAddItemBurstCommand : IItemRostersAddItemBurstCommand
{
    public string Prefix => "coop.debug.item_rosters";

    public string Name => "add_item_burst";

    public string Description => "Runs the add item burst debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("settlement_id", "The settlement StringId.", isRequired: true),
        new ExpectedArgs("count", "The positive number of items to add.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.ItemRosters.Commands.ItemRosterDebugCommands.AddItemBurst(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IItemRostersInfoCommand : ICoopCommand
{
}

public sealed class ItemRostersInfoCommand : IItemRostersInfoCommand
{
    public string Prefix => "coop.debug.item_rosters";

    public string Name => "info";

    public string Description => "Reports info.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("party_or_settlement_id", "The party or settlement StringId.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.ItemRosters.Commands.ItemRosterDebugCommands.Info(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IItemRostersExportCommand : ICoopCommand
{
}

public sealed class ItemRostersExportCommand : IItemRostersExportCommand
{
    public string Prefix => "coop.debug.item_rosters";

    public string Name => "export";

    public string Description => "Runs the export debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("party_or_settlement_id", "The party or settlement StringId.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.ItemRosters.Commands.ItemRosterDebugCommands.Export(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

#if DEBUG
public interface IPlayerCaptivityObserveAiLordPairCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityObserveAiLordPairCommand : IPlayerCaptivityObserveAiLordPairCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "observe_ai_lord_pair";

    public string Description => "Runs the observe ai lord pair debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("prisoner_hero_id", "The registered prisoner hero id.", isRequired: true),
        new ExpectedArgs("captor_hero_id", "The registered captor hero id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.AiLordPeaceReleaseFixtureCommands.ObserveAiLordPair(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IPlayerCaptivityFocusHeroPartyCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityFocusHeroPartyCommand : IPlayerCaptivityFocusHeroPartyCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "focus_hero_party";

    public string Description => "Runs the focus hero party debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_id", "The registered hero id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.AiLordPeaceReleaseFixtureCommands.FocusHeroParty(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IPlayerCaptivitySnapshotAiLordDiplomacyFixtureCommand : ICoopCommand
{
}

public sealed class PlayerCaptivitySnapshotAiLordDiplomacyFixtureCommand : IPlayerCaptivitySnapshotAiLordDiplomacyFixtureCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "snapshot_ai_lord_diplomacy_fixture";

    public string Description => "Runs the snapshot ai lord diplomacy fixture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("prisoner_hero_id", "The registered prisoner hero id.", isRequired: true),
        new ExpectedArgs("captor_hero_id", "The registered captor hero id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.AiLordPeaceReleaseFixtureCommands.SnapshotAiLordDiplomacyFixture(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IPlayerCaptivityRestoreAiLordDiplomacyFixtureCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityRestoreAiLordDiplomacyFixtureCommand : IPlayerCaptivityRestoreAiLordDiplomacyFixtureCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "restore_ai_lord_diplomacy_fixture";

    public string Description => "Runs the restore ai lord diplomacy fixture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.AiLordPeaceReleaseFixtureCommands.RestoreAiLordDiplomacyFixture(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IPlayerCaptivityCaptureAiLordFixtureCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityCaptureAiLordFixtureCommand : IPlayerCaptivityCaptureAiLordFixtureCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "capture_ai_lord_fixture";

    public string Description => "Runs the capture ai lord fixture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("prisoner_hero_id", "The registered prisoner hero id.", isRequired: true),
        new ExpectedArgs("captor_hero_id", "The registered captor hero id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.AiLordPeaceReleaseFixtureCommands.CaptureAiLordFixture(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IPlayerCaptivityObserveAiLordFixtureCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityObserveAiLordFixtureCommand : IPlayerCaptivityObserveAiLordFixtureCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "observe_ai_lord_fixture";

    public string Description => "Runs the observe ai lord fixture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.AiLordPeaceReleaseFixtureCommands.ObserveAiLordFixture(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IPlayerCaptivityFocusPartyCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityFocusPartyCommand : IPlayerCaptivityFocusPartyCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "focus_party";

    public string Description => "Runs the focus party debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("mobile_party_id", "The registered mobile party id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.AiLordPeaceReleaseFixtureCommands.FocusParty(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IPlayerCaptivityRestoreAiLordFixtureCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityRestoreAiLordFixtureCommand : IPlayerCaptivityRestoreAiLordFixtureCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "restore_ai_lord_fixture";

    public string Description => "Runs the restore ai lord fixture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.AiLordPeaceReleaseFixtureCommands.RestoreAiLordFixture(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}
#endif

public interface IPlayerCaptivityRandomCapturePlayerCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityRandomCapturePlayerCommand : IPlayerCaptivityRandomCapturePlayerCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "random_capture_player";

    public string Description => "Runs the random capture player debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.PlayerCaptivityCommands.RandomCapturePlayer(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IPlayerCaptivityCapturePlayerCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityCapturePlayerCommand : IPlayerCaptivityCapturePlayerCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "capture_player";

    public string Description => "Runs the capture player debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
        new ExpectedArgs("captor_party_id", "The registered captor mobile party id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.PlayerCaptivityCommands.CapturePlayer(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IPlayerCaptivityCapturePlayerFixtureCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityCapturePlayerFixtureCommand : IPlayerCaptivityCapturePlayerFixtureCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "capture_player_fixture";

    public string Description => "Runs the capture player fixture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
        new ExpectedArgs("captor_party_id", "The registered captor mobile party id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.PlayerCaptivityCommands.CapturePlayerFixture(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IPlayerCaptivityRestoreRosterFixtureCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityRestoreRosterFixtureCommand : IPlayerCaptivityRestoreRosterFixtureCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "restore_roster_fixture";

    public string Description => "Runs the restore roster fixture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.PlayerCaptivityCommands.RestoreRosterFixture(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IPlayerCaptivityReleasePlayerCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityReleasePlayerCommand : IPlayerCaptivityReleasePlayerCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "release_player";

    public string Description => "Runs the release player debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.PlayerCaptivityCommands.ReleasePlayer(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IPlayerCaptivityPrepareVisualTestFixtureCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityPrepareVisualTestFixtureCommand : IPlayerCaptivityPrepareVisualTestFixtureCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "prepare_visual_test_fixture";

    public string Description => "Runs the prepare visual test fixture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
        new ExpectedArgs("captor_party_id", "The registered captor mobile party id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.PlayerCaptivityCommands.PrepareVisualTestFixture(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IPlayerCaptivityRestoreVisualTestFixtureCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityRestoreVisualTestFixtureCommand : IPlayerCaptivityRestoreVisualTestFixtureCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "restore_visual_test_fixture";

    public string Description => "Runs the restore visual test fixture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.PlayerCaptivityCommands.RestoreVisualTestFixture(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IPlayerCaptivityLiberatePrisonerCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityLiberatePrisonerCommand : IPlayerCaptivityLiberatePrisonerCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "liberate_prisoner";

    public string Description => "Runs the liberate prisoner debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.PlayerCaptivityCommands.LiberatePrisoner(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IPlayerCaptivityStatusCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityStatusCommand : IPlayerCaptivityStatusCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "status";

    public string Description => "Reports status.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.PlayerCaptivityCommands.PrisonerStatus(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IPlayerCaptivityDiscardPlayerFromPartyScreenCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityDiscardPlayerFromPartyScreenCommand : IPlayerCaptivityDiscardPlayerFromPartyScreenCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "discard_player_from_party_screen";

    public string Description => "Runs the discard player from party screen debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
        new ExpectedArgs("captor_party_id", "The registered captor mobile party id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.PlayerCaptivityCommands.DiscardPlayerFromPartyScreen(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IPlayerCaptivityObservePlayerCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityObservePlayerCommand : IPlayerCaptivityObservePlayerCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "observe_player";

    public string Description => "Runs the observe player debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.PlayerCaptivityCommands.ObservePlayer(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IPlayerCaptivityRansomPlayerAtSettlementCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityRansomPlayerAtSettlementCommand : IPlayerCaptivityRansomPlayerAtSettlementCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "ransom_player_at_settlement";

    public string Description => "Runs the ransom player at settlement debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.PlayerCaptivityCommands.RansomPlayerAtSettlement(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IPlayerCaptivityCaptivityStateCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityCaptivityStateCommand : IPlayerCaptivityCaptivityStateCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "captivity_state";

    public string Description => "Reports captivity state.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.PlayerCaptivityCommands.CaptivityState(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IPlayerCaptivityPartyFixtureStateCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityPartyFixtureStateCommand : IPlayerCaptivityPartyFixtureStateCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "party_fixture_state";

    public string Description => "Reports party fixture state.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("party_id", "The registered mobile party id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.PlayerCaptivityCommands.PartyFixtureState(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IPlayerCaptivityRestorePartyFixtureStateCommand : ICoopCommand
{
}

public sealed class PlayerCaptivityRestorePartyFixtureStateCommand : IPlayerCaptivityRestorePartyFixtureStateCommand
{
    public string Prefix => "coop.debug.player_captivity";

    public string Name => "restore_party_fixture_state";

    public string Description => "Reports restore party fixture state.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("party_id", "The registered mobile party id.", isRequired: true),
        new ExpectedArgs("settlement_id_or_none", "The settlement id, or none.", isRequired: true),
        new ExpectedArgs("x", "The map x coordinate.", isRequired: true),
        new ExpectedArgs("y", "The map y coordinate.", isRequired: true),
        new ExpectedArgs("is_on_land", "Whether the position is on land.", isRequired: true),
        new ExpectedArgs("is_active", "Whether the party is active.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PlayerCaptivityService.Commands.PlayerCaptivityCommands.RestorePartyFixtureState(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ISaveSaveAsCommand : ICoopCommand
{
}

public sealed class SaveSaveAsCommand : ISaveSaveAsCommand
{
    public string Prefix => "coop.debug.save";

    public string Name => "save_as";

    public string Description => "Runs the save as debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("save_name", "A save name using 1 through 64 letters, digits, underscores, or hyphens.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Save.Commands.SaveDebugCommand.SaveAs(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ISaveStateCommand : ICoopCommand
{
}

public sealed class SaveStateCommand : ISaveStateCommand
{
    public string Prefix => "coop.debug.save";

    public string Name => "state";

    public string Description => "Reports state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Save.Commands.SaveDebugCommand.State(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ISaveForceAutosaveCommand : ICoopCommand
{
}

public sealed class SaveForceAutosaveCommand : ISaveForceAutosaveCommand
{
    public string Prefix => "coop.debug.save";

    public string Name => "force_autosave";

    public string Description => "Runs the force autosave debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("evidence_hold_milliseconds", "The optional evidence hold from 1 through 5000 milliseconds.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Save.Commands.SaveDebugCommand.ForceAutoSave(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICraftingGiveSuppliesCommand : ICoopCommand
{
}

public sealed class CraftingGiveSuppliesCommand : ICraftingGiveSuppliesCommand
{
    public string Prefix => "coop.debug.crafting";

    public string Name => "give_supplies";

    public string Description => "Runs the give supplies debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name", "The exact hero display name.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Smithing.Commands.SmithingCommands.SmithingSuppliesCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICraftingUnlockAllCraftingPiecesCommand : ICoopCommand
{
}

public sealed class CraftingUnlockAllCraftingPiecesCommand : ICraftingUnlockAllCraftingPiecesCommand
{
    public string Prefix => "coop.debug.crafting";

    public string Name => "unlock_all_crafting_pieces";

    public string Description => "Runs the unlock all crafting pieces debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Smithing.Commands.SmithingCommands.UnlockAllCraftingPiecesCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICraftingTownOrdersCommand : ICoopCommand
{
}

public sealed class CraftingTownOrdersCommand : ICraftingTownOrdersCommand
{
    public string Prefix => "coop.debug.crafting";

    public string Name => "town_orders";

    public string Description => "Runs the town orders debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("town_name", "The exact town display name.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Smithing.Commands.SmithingCommands.ViewTownOrdersCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICraftingAddTownOrderCommand : ICoopCommand
{
}

public sealed class CraftingAddTownOrderCommand : ICraftingAddTownOrderCommand
{
    public string Prefix => "coop.debug.crafting";

    public string Name => "add_town_order";

    public string Description => "Runs the add town order debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name", "The exact hero display name.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Smithing.Commands.SmithingCommands.AddTestingTownOrderCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICraftingAddCraftedItemsCommand : ICoopCommand
{
}

public sealed class CraftingAddCraftedItemsCommand : ICraftingAddCraftedItemsCommand
{
    public string Prefix => "coop.debug.crafting";

    public string Name => "add_crafted_items";

    public string Description => "Runs the add crafted items debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name", "The exact hero display name.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Smithing.Commands.SmithingCommands.AddCraftedItemCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICraftingStaminaCommand : ICoopCommand
{
}

public sealed class CraftingStaminaCommand : ICraftingStaminaCommand
{
    public string Prefix => "coop.debug.crafting";

    public string Name => "stamina";

    public string Description => "Runs the stamina debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Smithing.Commands.SmithingCommands.ViewCraftingStaminaCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICraftingCraftedItemHistoryCommand : ICoopCommand
{
}

public sealed class CraftingCraftedItemHistoryCommand : ICraftingCraftedItemHistoryCommand
{
    public string Prefix => "coop.debug.crafting";

    public string Name => "crafted_item_history";

    public string Description => "Runs the crafted item history debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Smithing.Commands.SmithingCommands.ViewCraftedItemHistoryCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICraftingCraftingPiecesXpCommand : ICoopCommand
{
}

public sealed class CraftingCraftingPiecesXpCommand : ICraftingCraftingPiecesXpCommand
{
    public string Prefix => "coop.debug.crafting";

    public string Name => "crafting_pieces_xp";

    public string Description => "Runs the crafting pieces xp debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Smithing.Commands.SmithingCommands.ViewPartsXpCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ICraftingUnlockedCraftingPiecesCommand : ICoopCommand
{
}

public sealed class CraftingUnlockedCraftingPiecesCommand : ICraftingUnlockedCraftingPiecesCommand
{
    public string Prefix => "coop.debug.crafting";

    public string Name => "unlocked_crafting_pieces";

    public string Description => "Runs the unlocked crafting pieces debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Smithing.Commands.SmithingCommands.ViewUnlockedCraftingPieces(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ITemplateCommand : ICoopCommand
{
}

public sealed class TemplateCommand : ITemplateCommand
{
    public string Prefix => "coop.debug";

    public string Name => "template";

    public string Description => "Runs the template debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Template.Commands.TemplateCommands.TemplateCommand(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface IGetTimeModeCommand : ICoopCommand
{
}

public sealed class GetTimeModeCommand : IGetTimeModeCommand
{
    public string Prefix => "coop.debug";

    public string Name => "get_time_mode";

    public string Description => "Reports get time mode.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Time.Commands.TimeCommands.GetTimeMode(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ISetTimeModeCommand : ICoopCommand
{
}

public sealed class SetTimeModeCommand : ISetTimeModeCommand
{
    public string Prefix => "coop.debug";

    public string Name => "set_time_mode";

    public string Description => "Runs the set time mode debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("time_mode", "Pause, Play_1x, or Play_2x.", isRequired: true),
#if DEBUG
        new ExpectedArgs("force_live_test", "The DEBUG live-test override token.", isRequired: false),
#endif
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Time.Commands.TimeCommands.SetTimeMode(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

#if DEBUG
public interface IRequestTimeModeCommand : ICoopCommand
{
}

public sealed class RequestTimeModeCommand : IRequestTimeModeCommand
{
    public string Prefix => "coop.debug";

    public string Name => "request_time_mode";

    public string Description => "Runs the request time mode debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("time_mode", "Pause, Play_1x, or Play_2x.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Time.Commands.TimeCommands.RequestTimeMode(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}
#endif

public interface IAdvanceTimeCommand : ICoopCommand
{
}

public sealed class AdvanceTimeCommand : IAdvanceTimeCommand
{
    public string Prefix => "coop.debug";

    public string Name => "advance_time";

    public string Description => "Runs the advance time debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("days", "The number of campaign days to advance.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Time.Commands.TimeCommands.AdvanceTime(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ISteamStatusCommand : ICoopCommand
{
}

public sealed class SteamStatusCommand : ISteamStatusCommand
{
    public string Prefix => "coop.debug.steam";

    public string Name => "status";

    public string Description => "Reports status.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.UI.Commands.SteamDebugCommand.Status(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ISteamHostLobbyCommand : ICoopCommand
{
}

public sealed class SteamHostLobbyCommand : ISteamHostLobbyCommand
{
    public string Prefix => "coop.debug.steam";

    public string Name => "host_lobby";

    public string Description => "Runs the host lobby debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.UI.Commands.SteamDebugCommand.HostLobby(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(
            output,
            "Steam integration inactive");
    }
}

public interface ISteamInviteCommand : ICoopCommand
{
}

public sealed class SteamInviteCommand : ISteamInviteCommand
{
    public string Prefix => "coop.debug.steam";

    public string Name => "invite";

    public string Description => "Runs the invite debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.UI.Commands.SteamDebugCommand.Invite(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}

public interface ISteamJoinCommand : ICoopCommand
{
}

public sealed class SteamJoinCommand : ISteamJoinCommand
{
    public string Prefix => "coop.debug.steam";

    public string Name => "join";

    public string Description => "Runs the join debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("lobby_id", "The Steam lobby id.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.UI.Commands.SteamDebugCommand.Join(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(
            output,
            "Steam integration inactive");
    }
}

public interface IUiTacticalSymbolsCommand : ICoopCommand
{
}

public sealed class UiTacticalSymbolsCommand : IUiTacticalSymbolsCommand
{
    public string Prefix => "coop.debug.ui";

    public string Name => "tactical_symbols";

    public string Description => "Runs the tactical symbols debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("mode", "on, off, toggle, or status.", isRequired: true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.UI.Commands.TacticalUnitSymbolsDebugCommand.TacticalSymbols(new List<string>(args));
        return SystemDeveloperLegacyCommandResult.FromOutput(output);
    }
}
