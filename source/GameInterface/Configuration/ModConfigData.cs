using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace GameInterface.Configuration;

/// <summary>
/// THE mod-config.json schema: one explicit property per key — the class IS the
/// schema, adding a property adds the key. Gameplay/mod settings and local mission
/// network capacity only; process settings (ports, saves, passwords) live in the
/// dedicated server's server-config.json. Nullable means the owning consumer uses
/// its documented default or leaves the loaded world's value alone. Keys the file has that the schema
/// doesn't land in <see cref="UnknownKeys"/> for the load warning.
/// </summary>
public sealed class ModConfigData
{
    /// <summary>Campaign difficulty block, applied by CampaignDifficultyHandler
    /// on the hosting side once the campaign is up.</summary>
    public DifficultyConfigData Difficulty { get; set; } = new DifficultyConfigData();

    public ModOptionsData ModOptions { get; set; } = new ModOptionsData();

    /// <summary>Local client network capacity. Dedicated servers do not originate mission movement.</summary>
    public NetworkConfigData Network { get; set; } = new NetworkConfigData();

    [JsonExtensionData]
    public IDictionary<string, JToken> UnknownKeys { get; set; }
}

/// <summary>
/// The "difficulty" block. Properties remain nullable so malformed or unreadable
/// input can fall back safely, while ModConfig activates or fills the documented
/// values before a normal load. CampaignOptions values live IN the save, so joining
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

/// <summary>Local mission movement bandwidth limits for this game process.</summary>
public sealed class NetworkConfigData
{
    /// <summary>Total movement payload this client may send per second.</summary>
    public double? MovementOutgoingMiBPerSecond { get; set; }

    /// <summary>Total movement payload this client asks all mission peers to send per second.</summary>
    public double? MovementIncomingMiBPerSecond { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken> UnknownKeys { get; set; }
}

public sealed class ModOptionsData
{
    public bool? FastForwardEnabled { get; set; }

    public bool? AutoPauseEnabled { get; set; }

    public bool? ClientsCanUseCheats { get; set; }

    public bool? GoldFoodInfluenceChangeInSettlements { get; set; }

    public GoldFoodChangeMode? GoldFoodInfluenceChangeInBattles { get; set; }

    public bool? GoldFoodInfluenceChangeForDisconnectedPlayers { get; set; }

    public int? PlayerBattleAiJoinWindowHours { get; set; }

    public bool? SpeedLimitWhilePlayersInBattle { get; set; }

    public int? WandererLimit { get; set; }

    public bool? WandererLimitScalesWithPlayers { get; set; }

    public int? PlayerKingdomClanTierRequired { get; set; }

    public bool? SmithingStaminaRecoveryOutsideSettlements { get; set; }

    public float? SmithingStaminaRecoveryMultiplier { get; set; }

    public float? MaximumLootersMultiplier { get; set; }

    public float? LooterPartySizeMultiplier { get; set; }

    public LordDefectionRetryMode? LordDefectionRetries { get; set; }
  
    public bool? EnableHeroExecutions { get; set; }

    public bool? EnablePlayerClanMemberExecutions { get; set; }

    public bool? ShowPlayerNameplates { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken> UnknownKeys { get; set; }
}

/// <summary>
/// How long a lord remembers refusing a recruitment attempt.
/// </summary>
/// <remarks>
/// Vanilla keeps each attempt for one in-game year and blocks the lord until it expires. That
/// bookkeeping is filled in only by the machine that ran the conversation, so co-op has to choose
/// what a client's list means.
/// </remarks>
public enum LordDefectionRetryMode
{
    /// <summary>Vanilla: a refusal blocks that lord for one in-game year, then expires.</summary>
    Vanilla,

    /// <summary>A refusal blocks that lord for the rest of the session.</summary>
    NeverExpire,

    /// <summary>A refusal never blocks; the lord can be asked again straight away.</summary>
    AlwaysRetry
}

public enum GoldFoodChangeMode
{
    Disabled,
    OneDayMax,
    Enabled
}
