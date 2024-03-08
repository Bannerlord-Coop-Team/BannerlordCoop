using Common;
using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.ObjectManager.Extensions;

/// <summary>
/// Extension methods for <see cref="CampaignObjectManager"/>
/// </summary>
internal static class CampaignObjectManagerExtensions
{
    public static IEnumerable<Hero> GetAllHeroes(this CampaignObjectManager campaignObjectManager)
    {
        return campaignObjectManager.AliveHeroes.Concat(campaignObjectManager.DeadOrDisabledHeroes);
    }

    public static void OnHeroAdded(this CampaignObjectManager campaignObjectManager, Hero hero)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            campaignObjectManager.OnHeroAdded(hero);
        });
    }

    public static void AddMobileParty(this CampaignObjectManager campaignObjectManager, MobileParty party)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            campaignObjectManager.AddMobileParty(party);
        });
    }
}
