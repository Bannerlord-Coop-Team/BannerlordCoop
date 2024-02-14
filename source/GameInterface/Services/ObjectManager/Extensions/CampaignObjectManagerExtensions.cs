using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.ObjectManager.Extensions;
internal static class CampaignObjectManagerExtensions
{
    private static readonly Action<CampaignObjectManager, Hero> OnHeroAddedDelegate = typeof(CampaignObjectManager)
        .GetMethod("OnHeroAdded", BindingFlags.NonPublic | BindingFlags.Instance)
        .BuildDelegate<Action<CampaignObjectManager, Hero>>();

    public static IEnumerable<Hero> GetAllHeroes(this CampaignObjectManager campaignObjectManager)
    {
        return campaignObjectManager.AliveHeroes.Concat(campaignObjectManager.DeadOrDisabledHeroes);
    }

    public static void OnHeroAdded(this CampaignObjectManager campaignObjectManager, Hero hero)
    {
        OnHeroAddedDelegate(campaignObjectManager, hero);
    }
}
