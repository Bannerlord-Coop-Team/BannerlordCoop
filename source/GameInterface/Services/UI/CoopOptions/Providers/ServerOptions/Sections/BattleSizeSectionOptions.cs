using System.Text.Json.Serialization;

namespace GameInterface.Services.UI.CoopOptions.Providers.ServerOptions.Sections;

/// <summary>Persists the server's selected battle-size slider value.</summary>
public class BattleSizeSectionOptions
{
    [JsonPropertyName("battleSize")]
    public int BattleSize { get; set; }

    public static BattleSizeSectionOptions FromBattleSize(int battleSize)
    {
        return new BattleSizeSectionOptions
        {
            BattleSize = ServerOptionsTabProvider.NormalizeBattleSize(battleSize)
        };
    }

    public bool TryGetBattleSize(out int battleSize)
    {
        battleSize = BattleSize;
        return ServerOptionsTabProvider.IsSupportedBattleSize(battleSize);
    }
}
