using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Configuration;

/// <summary>
/// THE mod-config.json schema: one explicit property per key — the class IS the
/// schema, adding a property adds the key. Gameplay/mod settings only; how the
/// server process runs (ports, saves, passwords) is the dedicated server's
/// server-config.json and never appears here. Nullable properties mean "absent =
/// leave the loaded world's value alone". Keys the file has that the schema
/// doesn't land in <see cref="UnknownKeys"/> for the load warning.
/// </summary>
public sealed class ModConfigData
{
    /// <summary>Campaign difficulty block, applied by CampaignDifficultyHandler
    /// on the hosting side once the campaign is up.</summary>
    public DifficultyConfigData Difficulty { get; set; } = new DifficultyConfigData();

    public ModOptionsData ModOptions { get; set; } = new ModOptionsData();

    [JsonExtensionData]
    public IDictionary<string, JToken> UnknownKeys { get; set; }
}

/// <summary>
/// The "difficulty" block. Every key nullable: absent (or commented out in the
/// template) keeps the save's own setting. The CampaignOptions values live IN
/// the save, so joining clients inherit them with the join-time save transfer —
/// playerReceivedDamage is the exception (engine config, pushed to clients as a
/// join-time ServerOption; see CampaignDifficultyHandler for its headless-host
/// default).
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
    public bool? FastForwardEnabled { get; set; }

    public bool? AutoPauseEnabled { get; set; }

    public bool? GoldFoodInfluenceChangeInSettlements { get; set; }

    public GoldFoodChangeMode? GoldFoodInfluenceChangeInBattles { get; set; }

    public bool? GoldFoodInfluenceChangeForDisconnectedPlayers { get; set; }

    public int? PlayerBattleAiJoinWindowHours { get; set; }

    public bool? SpeedLimitWhilePlayersInBattle { get; set; }

    public int? WandererLimit { get; set; }

    public int? PlayerKingdomClanTierRequired { get; set; }

    public bool? SmithingStaminaRecoveryOutsideSettlements { get; set; }

    public float? SmithingStaminaRecoveryMultiplier { get; set; }

    public float? MaximumBanditsMultiplier { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken> UnknownKeys { get; set; }
}

public enum GoldFoodChangeMode
{
    Disabled,
    OneDayMax,
    Enabled
}