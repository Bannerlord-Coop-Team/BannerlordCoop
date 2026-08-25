using ProtoBuf;

namespace GameInterface.Configuration;

public class ModConfigProvider
{
    /// <summary>What the session runs on until a config is loaded (server) or received (client).
    /// Built from an all-absent <see cref="ModOptionsData"/> so every option falls back to its
    /// documented default. It has to go through that constructor: the options struct declares no
    /// parameterless one, so a plain <c>new ModOptions()</c> is just <c>default</c> — the property
    /// initializers below never run and every option reads back false/0.</summary>
    public static ModOptions ModOptions = new(new ModOptionsData());

    public static void LoadModConfig(ModOptionsData modOptionsData)
    {
        ModOptions = new(modOptionsData);
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct ModOptions
{
    [ProtoMember(1)]
    public readonly bool FastForwardEnabled { get; } = true;
    [ProtoMember(2)]
    public readonly bool AutoPauseEnabled { get; } = true;
    [ProtoMember(3)]
    public readonly bool ClientsCanUseCheats { get; } = false;
    [ProtoMember(4)]
    public readonly bool GoldFoodInfluenceChangeInSettlements { get; } = true;
    [ProtoMember(5)]
    public readonly GoldFoodChangeMode GoldFoodInfluenceChangeInBattles { get; } = GoldFoodChangeMode.OneDayMax;
    [ProtoMember(6)]
    public readonly bool GoldFoodInfluenceChangeForDisconnectedPlayers { get; } = false;
    [ProtoMember(7)]
    public readonly int PlayerBattleAiJoinWindowHours { get; } = 24;
    [ProtoMember(8)]
    public readonly bool SpeedLimitWhilePlayersInBattle { get; } = true;
    [ProtoMember(9)]
    public readonly int WandererLimit { get; } = 32;
    [ProtoMember(10)]
    public readonly bool WandererLimitScalesWithPlayers { get; } = false;
    [ProtoMember(11)]
    public readonly int PlayerKingdomClanTierRequired { get; } = 4;
    [ProtoMember(12)]
    public readonly bool SmithingStaminaRecoveryOutsideSettlements { get; } = true;
    [ProtoMember(13)]
    public readonly float SmithingStaminaRecoveryMultiplier { get; } = 0.1f;
    [ProtoMember(14)]
    public readonly float MaximumLootersMultiplier { get; } = 1f;
    [ProtoMember(15)]
    public readonly float LooterPartySizeMultiplier { get; } = 1f;
    [ProtoMember(16)]
    public readonly LordDefectionRetryMode LordDefectionRetries { get; } = LordDefectionRetryMode.Vanilla;
    [ProtoMember(17)]  
    public readonly bool EnableHeroExecutions { get; } = true;
    [ProtoMember(18)]
    public readonly bool EnablePlayerClanMemberExecutions { get; } = false;
    [ProtoMember(19)]
    public readonly bool ShowPlayerNameplates { get; } = true;
    [ProtoMember(20)]
    public readonly bool PlayerWoundedBattleEntry { get; } = true;
    [ProtoMember(21)]
    public readonly int MinimumWarDurationDays { get; } = 0;
    [ProtoMember(22)]
    public readonly int PeaceDeclineCooldownDays { get; } = 0;

    public ModOptions(ModOptionsData modOptionsData)
    {
        FastForwardEnabled = modOptionsData.FastForwardEnabled ?? FastForwardEnabled;
        AutoPauseEnabled = modOptionsData.AutoPauseEnabled ?? AutoPauseEnabled;
        ClientsCanUseCheats = modOptionsData.ClientsCanUseCheats ?? ClientsCanUseCheats;
        GoldFoodInfluenceChangeInSettlements = modOptionsData.GoldFoodInfluenceChangeInSettlements ?? GoldFoodInfluenceChangeInSettlements;
        GoldFoodInfluenceChangeInBattles = modOptionsData.GoldFoodInfluenceChangeInBattles ?? GoldFoodInfluenceChangeInBattles;
        GoldFoodInfluenceChangeForDisconnectedPlayers = modOptionsData.GoldFoodInfluenceChangeForDisconnectedPlayers ?? GoldFoodInfluenceChangeForDisconnectedPlayers;
        PlayerBattleAiJoinWindowHours = modOptionsData.PlayerBattleAiJoinWindowHours ?? PlayerBattleAiJoinWindowHours;
        SpeedLimitWhilePlayersInBattle = modOptionsData.SpeedLimitWhilePlayersInBattle ?? SpeedLimitWhilePlayersInBattle;
        WandererLimit = modOptionsData.WandererLimit ?? WandererLimit;
        WandererLimitScalesWithPlayers = modOptionsData.WandererLimitScalesWithPlayers ?? WandererLimitScalesWithPlayers;
        PlayerKingdomClanTierRequired = modOptionsData.PlayerKingdomClanTierRequired ?? PlayerKingdomClanTierRequired;
        SmithingStaminaRecoveryOutsideSettlements = modOptionsData.SmithingStaminaRecoveryOutsideSettlements ?? SmithingStaminaRecoveryOutsideSettlements;
        SmithingStaminaRecoveryMultiplier = modOptionsData.SmithingStaminaRecoveryMultiplier ?? SmithingStaminaRecoveryMultiplier;
        MaximumLootersMultiplier = modOptionsData.MaximumLootersMultiplier ?? MaximumLootersMultiplier;
        LooterPartySizeMultiplier = modOptionsData.LooterPartySizeMultiplier ?? LooterPartySizeMultiplier;
        LordDefectionRetries = modOptionsData.LordDefectionRetries ?? LordDefectionRetries;
        EnableHeroExecutions = modOptionsData.EnableHeroExecutions ?? EnableHeroExecutions;
        EnablePlayerClanMemberExecutions = modOptionsData.EnablePlayerClanMemberExecutions ?? EnablePlayerClanMemberExecutions;
        ShowPlayerNameplates = modOptionsData.ShowPlayerNameplates ?? ShowPlayerNameplates;
        PlayerWoundedBattleEntry = modOptionsData.PlayerWoundedBattleEntry ?? PlayerWoundedBattleEntry;
        MinimumWarDurationDays = modOptionsData.MinimumWarDurationDays ?? MinimumWarDurationDays;
        PeaceDeclineCooldownDays = modOptionsData.PeaceDeclineCooldownDays ?? PeaceDeclineCooldownDays;
    }
}
