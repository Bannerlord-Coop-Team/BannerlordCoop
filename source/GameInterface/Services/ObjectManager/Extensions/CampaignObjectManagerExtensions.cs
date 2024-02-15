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
        OnHeroAddedDelegate(campaignObjectManager, hero);
    }

    public static void AddMobileParty(this CampaignObjectManager campaignObjectManager, MobileParty party)
    {
        AddMobilePartyDelegate(campaignObjectManager, party);
    }

    private static readonly Action<CampaignObjectManager, Hero> OnHeroAddedDelegate = typeof(CampaignObjectManager)
        .GetMethod("OnHeroAdded", BindingFlags.NonPublic | BindingFlags.Instance)
        .BuildDelegate<Action<CampaignObjectManager, Hero>>();

    private static readonly Action<CampaignObjectManager, MobileParty> AddMobilePartyDelegate = typeof(CampaignObjectManager)
        .GetMethod("AddMobileParty", BindingFlags.NonPublic | BindingFlags.Instance)
        .BuildDelegate<Action<CampaignObjectManager, MobileParty>>();
}
