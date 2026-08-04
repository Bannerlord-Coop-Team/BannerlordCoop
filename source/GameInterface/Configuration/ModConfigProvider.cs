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
    public readonly LordDefectionRetryMode LordDefectionRetries { get; } = LordDefectionRetryMode.Vanilla;
    [ProtoMember(16)]
    public readonly bool MilitiaJoinsSallyOut { get; } = true;
    [ProtoMember(17)]
    public readonly bool ResumeSiegeWhenEnemyRetreats { get; } = true;
    [ProtoMember(18)]
    public readonly bool GarrisonJoinsSiegeRelief { get; } = true;

    /// <summary>
    /// Takes the configured value, or keeps the option's declared default when the config leaves it out.
    /// Spelled as a call rather than <c>??</c> so the constructor reads as one straight line per option:
    /// seventeen null-coalesces in a row look like seventeen branches, both to an analyzer and to anyone
    /// skimming for the single option they care about.
    /// </summary>
    private static T Or<T>(T? configured, T declaredDefault) where T : struct => configured ?? declaredDefault;

    public ModOptions(ModOptionsData modOptionsData)
    {
        FastForwardEnabled = Or(modOptionsData.FastForwardEnabled, FastForwardEnabled);
        AutoPauseEnabled = Or(modOptionsData.AutoPauseEnabled, AutoPauseEnabled);
        ClientsCanUseCheats = Or(modOptionsData.ClientsCanUseCheats, ClientsCanUseCheats);
        GoldFoodInfluenceChangeInSettlements = Or(modOptionsData.GoldFoodInfluenceChangeInSettlements, GoldFoodInfluenceChangeInSettlements);
        GoldFoodInfluenceChangeInBattles = Or(modOptionsData.GoldFoodInfluenceChangeInBattles, GoldFoodInfluenceChangeInBattles);
        GoldFoodInfluenceChangeForDisconnectedPlayers = Or(modOptionsData.GoldFoodInfluenceChangeForDisconnectedPlayers, GoldFoodInfluenceChangeForDisconnectedPlayers);
        PlayerBattleAiJoinWindowHours = Or(modOptionsData.PlayerBattleAiJoinWindowHours, PlayerBattleAiJoinWindowHours);
        SpeedLimitWhilePlayersInBattle = Or(modOptionsData.SpeedLimitWhilePlayersInBattle, SpeedLimitWhilePlayersInBattle);
        WandererLimit = Or(modOptionsData.WandererLimit, WandererLimit);
        WandererLimitScalesWithPlayers = Or(modOptionsData.WandererLimitScalesWithPlayers, WandererLimitScalesWithPlayers);
        PlayerKingdomClanTierRequired = Or(modOptionsData.PlayerKingdomClanTierRequired, PlayerKingdomClanTierRequired);
        SmithingStaminaRecoveryOutsideSettlements = Or(modOptionsData.SmithingStaminaRecoveryOutsideSettlements, SmithingStaminaRecoveryOutsideSettlements);
        SmithingStaminaRecoveryMultiplier = Or(modOptionsData.SmithingStaminaRecoveryMultiplier, SmithingStaminaRecoveryMultiplier);
        MaximumLootersMultiplier = Or(modOptionsData.MaximumLootersMultiplier, MaximumLootersMultiplier);
        LordDefectionRetries = Or(modOptionsData.LordDefectionRetries, LordDefectionRetries);
        MilitiaJoinsSallyOut = Or(modOptionsData.MilitiaJoinsSallyOut, MilitiaJoinsSallyOut);
        ResumeSiegeWhenEnemyRetreats = Or(modOptionsData.ResumeSiegeWhenEnemyRetreats, ResumeSiegeWhenEnemyRetreats);
        GarrisonJoinsSiegeRelief = Or(modOptionsData.GarrisonJoinsSiegeRelief, GarrisonJoinsSiegeRelief);
    }
}