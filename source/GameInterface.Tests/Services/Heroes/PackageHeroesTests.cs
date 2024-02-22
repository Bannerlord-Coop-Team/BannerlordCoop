using Autofac;
using GameInterface.Services.Registry;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Extensions;
using System;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Heroes
{
    public class RetrieveHeroAssociationsTests : IDisposable
    {
        // Number of heroes to create for each test
        // Must be greater than 0
        private const int NUM_HEROES = 2;

        private readonly PatchBootstrap bootstrap;
        private IContainer Container => bootstrap.Container;
        public RetrieveHeroAssociationsTests()
        {
            bootstrap = new PatchBootstrap();
        }

        public void Dispose() => bootstrap.Dispose();

        [Fact]
        public void RegisterHeroes()
        {
            // Setup
            var heroRegistry = Container.Resolve<HeroRegistry>();
            var heroes = new Hero[NUM_HEROES];

            for (int i = 0; i < NUM_HEROES; i++)
            {
                var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                hero.StringId = $"Hero {i}";

                Campaign.Current.CampaignObjectManager.AddHero(hero);

                heroes[i] = hero;
            }

            // Execution
            heroRegistry.RegisterAll();

            // Verification
            foreach (var hero in heroes)
            {
                Assert.True(heroRegistry.TryGetId(hero, out string _));
            }
        }
    }
}
