using Common;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes
{
    internal interface IHeroRegistry : IRegistryBase<Hero>
    {
    }

    internal class HeroRegistry : RegistryBase<Hero>, IHeroRegistry
    {
    }
}
