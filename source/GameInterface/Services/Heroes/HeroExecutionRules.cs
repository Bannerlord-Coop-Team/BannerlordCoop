using GameInterface.Configuration;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

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
            reason = new TextObject("{=str_coop_cannot_execute_heroes}Executing heroes has been disabled by the host.").ToString();
            return false;
        }

#if TESTER
        if (!ModConfigProvider.ModOptions.EnablePlayerExecutions
            && hero?.IsPlayerHero() == true)
#else
        if (hero?.IsPlayerHero() == true)
#endif
        {
            reason = new TextObject("{=str_coop_cannot_execute_players}Executing player heroes has been disabled by the host.").ToString();
            return false;
        }

        if (hero?.IsPlayerHero() != true
            && !ModConfigProvider.ModOptions.EnablePlayerClanMemberExecutions
            && hero?.Clan?.IsPlayerClan() == true)
        {
            reason = new TextObject("{=str_coop_cannot_execute_player_clan_members}Executing members of other players' clans has been disabled by the host.").ToString();
            return false;
        }

        reason = null;
        return true;
    }
}
