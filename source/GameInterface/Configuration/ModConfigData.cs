using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace GameInterface.Configuration;

/// <summary>Describes a managed option for the shipped catalog. Specify <see cref="AvailableSince"/> for every option.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ManagedConfigOptionAttribute : Attribute
{
    public string AvailableSince { get; set; }
    // NaN leaves the bound unset.
    public double Min { get; set; } = double.NaN;
    public double Max { get; set; } = double.NaN;
    /// <summary>Regex the website applies to each item of a list option.</summary>
    public string ItemPattern { get; set; }
    /// <summary>Item kind of a list option for the website; only "steamId64" is accepted.</summary>
    public string ItemFormat { get; set; }
    /// <summary>Raises positive values below this threshold to the threshold and leaves zero unchanged.</summary>
    public int CoercePositiveBelow { get; set; }
}

/// <summary>
/// THE mod-config.json schema: one explicit property per key — the class IS the
/// schema, adding a property adds the key. Gameplay/mod settings only; process
/// settings (ports, saves, passwords) live in the dedicated server's
/// server-config.json. Nullable means the owning consumer uses
/// its documented default or leaves the loaded world's value alone. Keys the file has that the schema
/// doesn't land in <see cref="UnknownKeys"/> for the load warning.
/// </summary>
public sealed class ModConfigData
{
    /// <summary>Campaign difficulty block, applied by CampaignDifficultyHandler
    /// on the hosting side once the campaign is up.</summary>
    public DifficultyConfigData Difficulty { get; set; } = new DifficultyConfigData();

    public ModOptionsData ModOptions { get; set; } = new ModOptionsData();

    public VoiceConfigData Voice { get; set; } = new VoiceConfigData();

    public AdvancedStartConfigData AdvancedStart { get; set; } = new AdvancedStartConfigData();

    [JsonExtensionData]
    public IDictionary<string, JToken> UnknownKeys { get; set; }
}

/// <summary>
/// The "advancedStart" block: which vanilla advanced starting options a joining player
/// is offered after character creation. Read by AdvancedStartInterface on the server.
/// </summary>
public sealed class AdvancedStartConfigData
{
    /// <summary>false: joining players never see the starting options window. Defaults to true.</summary>
    public bool? Enabled { get; set; }

    /// <summary>The start type selected when the window opens. Defaults to vanilla's "default".</summary>
    public string DefaultStartType { get; set; }

    public AdvancedStartTypesConfigData StartTypes { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken> UnknownKeys { get; set; }
}

/// <summary>
/// The "advancedStart.startTypes" block, one key per vanilla start type. false hides that start type
/// from the window, except Default, which stays listed but locked; a missing key offers it.
/// </summary>
public sealed class AdvancedStartTypesConfigData
{
    /// <summary>false: the default start is only offered while no other start type can be picked.</summary>
    public bool? Default { get; set; }
    public bool? Outlaw { get; set; }
    public bool? Beggar { get; set; }
    public bool? Trader { get; set; }
    public bool? Mercenary { get; set; }
    public bool? Vassal { get; set; }
    public bool? King { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken> UnknownKeys { get; set; }
}

/// <summary>
/// The "difficulty" block. Properties remain nullable so malformed or unreadable
/// input can fall back safely. ModConfig adds settings missing from the shipped template
/// before a normal load, while commented-out keys stay null. CampaignOptions values live IN the save, so joining
/// clients inherit them with the join-time save transfer. PlayerReceivedDamage is
/// engine config and is pushed to clients as a join-time ServerOption.
/// </summary>
public sealed class DifficultyConfigData
{
    public DifficultyLevel? PlayerReceivedDamage { get; set; }
    public DifficultyLevel? PlayerTroopsReceivedDamage { get; set; }
    public DifficultyLevel? CombatAIDifficulty { get; set; }
    public DifficultyLevel? RecruitmentDifficulty { get; set; }
    public DifficultyLevel? PlayerMapMovementSpeed { get; set; }
    public DifficultyLevel? StealthAndDisguiseDifficulty { get; set; }
    public DifficultyLevel? PersuasionSuccessChance { get; set; }
    public DifficultyLevel? ClanMemberDeathChance { get; set; }
    public DifficultyLevel? BattleDeath { get; set; }

    /// <summary>Positive polarity for the operator: true = the world ages
    /// (births, natural deaths) — the engine flag is the inverse.</summary>
    public bool? BirthAndDeath { get; set; }

    public bool? AutoAllocateClanMemberPerks { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken> UnknownKeys { get; set; }
}

/// <summary>Difficulty levels as the game names them; serialized as strings
/// ("VeryEasy" | "Easy" | "Realistic", case-insensitive) in mod-config.json.
/// Mirrors TaleWorlds' CampaignOptions.Difficulty (mapped by name, not ordinal,
/// in CampaignDifficultyHandler).</summary>
public enum DifficultyLevel
{
    VeryEasy,
    Easy,
    Realistic,
}

public sealed class ModOptionsData
{
    public const int MinimumBattleSize = 200;
    public const int MaximumBattleSize = 1000;

    // Give a player time to read the dialog even with a very short timer.
    internal const int MinimumPvpResponseTimerSeconds = 10;

    [ManagedConfigOption(AvailableSince = "0.1.6")]
    public bool? VoiceEnabled { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.3", Min = MinimumBattleSize, Max = MaximumBattleSize)]
    public int? BattleSize { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public bool? FastForwardEnabled { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public bool? AutoPauseEnabled { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.8")]
    public bool? AllowClientTimeControls { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.9")]
    public bool? MapClickChangesTime { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public bool? ClientsCanUseCheats { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public bool? GoldFoodInfluenceChangeInSettlements { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public GoldFoodChangeMode? GoldFoodInfluenceChangeInBattles { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public bool? GoldFoodInfluenceChangeForDisconnectedPlayers { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.6")]
    public bool? BlockAiWarDeclarationsOnOfflinePlayers { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public int? PlayerBattleAiJoinWindowHours { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.8")]
    public int? PostBattleAiAmnestySeconds { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public bool? SpeedLimitWhilePlayersInBattle { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.6")]
    public bool? EnsureUnaffiliatedWanderers { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public int? WandererLimit { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.7")]
    public int? SpawnRateWanderers { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public bool? WandererLimitScalesWithPlayers { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public int? PlayerKingdomClanTierRequired { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.6")]
    public bool? CoopClansEnabled { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public bool? SmithingStaminaRecoveryOutsideSettlements { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public float? SmithingStaminaRecoveryMultiplier { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public float? MaximumLootersMultiplier { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.3")]
    public float? LooterPartySizeMultiplier { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public LordDefectionRetryMode? LordDefectionRetries { get; set; }
  
    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public bool? EnableHeroExecutions { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.2")]
    public bool? EnablePlayerClanMemberExecutions { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.6")]
    public bool? EnablePlayerExecutions { get; set; }
    
    [ManagedConfigOption(AvailableSince = "0.1.4")]
    public bool? PlayerWoundedBattleEntry { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.3")]
    public bool? ShowPlayerNameplates { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.7")]
    public int? KingdomDecisionVoteTimeoutSeconds { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.8")]
    public ClansAfterKingdomDestructionMode? ClansAfterKingdomDestruction { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.8", ItemPattern = "^[0-9]{17}$", ItemFormat = "steamId64")]
    public List<string> PreferredBattleHosts { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.8")]
    public bool? PvpBlockLeave { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.8", Min = 0, CoercePositiveBelow = MinimumPvpResponseTimerSeconds)]
    public int? PvpResponseTimerSeconds { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.8")]
    public int? MaxClansPerKingdom { get; set; }

    [ManagedConfigOption(AvailableSince = "0.1.8")]
    public bool? MaxClansPerKingdomCountsMercenaries { get; set; }

    /// <summary>Whether a dedicated server runs War Sails (NavalDLC) co-op. It parses the file itself; declared so the key isn't reported as unknown.</summary>
    [ManagedConfigOption(AvailableSince = "0.1.8")]
    public bool? WarSails { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken> UnknownKeys { get; set; }
}

/// <summary>
/// How long a lord remembers refusing a recruitment attempt.
/// </summary>
/// <remarks>
/// Vanilla blocks the lord for one in-game season after a refused attempt. That
/// bookkeeping is filled in only by the machine that ran the conversation, so co-op has to choose
/// what a client's list means.
/// </remarks>
public enum LordDefectionRetryMode
{
    /// <summary>Vanilla: a refusal blocks that lord for one in-game season, then expires.</summary>
    Vanilla,

    /// <summary>A refusal blocks that lord for the rest of the session.</summary>
    NeverExpire,

    /// <summary>A refusal never blocks; the lord can be asked again straight away.</summary>
    AlwaysRetry
}

/// <summary>
/// What happens to clans left without a kingdom after their kingdom is destroyed.
/// </summary>
public enum ClansAfterKingdomDestructionMode
{
    Vanilla,
    KeepClans
}

public enum GoldFoodChangeMode
{
    Disabled,
    OneDayMax,
    Enabled
}
