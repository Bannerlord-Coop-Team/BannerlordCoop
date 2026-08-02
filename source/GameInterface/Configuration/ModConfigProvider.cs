using ProtoBuf;

namespace GameInterface.Configuration;

public class ModConfigProvider
{
    public static ModOptions ModOptions = new();

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
    public readonly bool SpeedLimitWhilePlayersInBattle { get; } = false;
    [ProtoMember(9)]
    public readonly int WandererLimit { get; } = 32;
    [ProtoMember(10)]
    public readonly int PlayerKingdomClanTierRequired { get; } = 4;
    [ProtoMember(11)]
    public readonly bool SmithingStaminaRecoveryOutsideSettlements { get; } = true;
    [ProtoMember(12)]
    public readonly float SmithingStaminaRecoveryMultiplier { get; } = 0.1f;
    [ProtoMember(13)]
    public readonly float MaximumBanditsMultiplier { get; } = 1f;

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
        PlayerKingdomClanTierRequired = modOptionsData.PlayerKingdomClanTierRequired ?? PlayerKingdomClanTierRequired;
        SmithingStaminaRecoveryOutsideSettlements = modOptionsData.SmithingStaminaRecoveryOutsideSettlements ?? SmithingStaminaRecoveryOutsideSettlements;
        SmithingStaminaRecoveryMultiplier = modOptionsData.SmithingStaminaRecoveryMultiplier ?? SmithingStaminaRecoveryMultiplier;
        MaximumBanditsMultiplier = modOptionsData.MaximumBanditsMultiplier ?? MaximumBanditsMultiplier;
    }
}