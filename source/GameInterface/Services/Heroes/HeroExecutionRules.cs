using GameInterface.Configuration;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes;

public static class HeroExecutionRules
{
    /// <summary>
    /// Try to get a blocked reason for hero execution based on config.
    /// </summary>
    public static bool IsExecutable(Hero hero, out string reason)
    {
        if (!ModConfigProvider.ModOptions.EnableHeroExecutions)
        {
            // TODO: Replace with localization "str_coop_cannot_execute_heroes"
            reason = "Executing heroes has been disabled by the host.";
            return false;
        }

        if (hero?.IsPlayerHero() == true) // TODO: Config for toggling player executions when player deaths supported
        {
            // TODO: Replace with localization "str_coop_cannot_execute_players"
            reason = "Executing players is disabled in Co-op for now.";
            return false;
        }

        if (!ModConfigProvider.ModOptions.EnablePlayerClanMemberExecutions
            && hero?.Clan?.IsPlayerClan() == true)
        {
            // TODO: Replace with localization "str_coop_cannot_execute_player_clan_members"
            reason = "Executing members of other players' clans has been disabled by the host.";
            return false;
        }

        reason = null;
        return true;
    }
}
