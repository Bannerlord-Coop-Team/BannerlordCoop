namespace GameInterface.Services.MapEvents;

internal static class MapEventConfig
{
    public const bool Enabled = true;
    public const bool Debug = true;

    // How long after a player's battle begins that AI parties may still join it as reinforcements. One
    // campaign day; after it passes, AI can no longer join a player's battle (see Postfix_CanPartyJoinBattle).
    public const int PlayerBattleAiJoinWindowHours = 24;
}
