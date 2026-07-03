using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Patches;

internal class ResolvedMainHeroContext
{
    [ThreadStatic]
    internal static Hero? ResolvedMainHero;
}
