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
        return campaignObjectManager.DeadOrDisabledHeroes.Concat(campaignObjectManager.AliveHeroes);
    }
}
