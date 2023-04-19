using Common.Logging;
using GameInterface.Services.Heroes;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Save.Data;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Save.Interfaces
{
    internal interface IRegistryInterface : IGameAbstraction
    {
        string[] GetControlledHeroIds();
        void RegisterAllGameObjects();
        void RegisterControlledHeroes(IEnumerable<string> heroIds);
    }
    internal class RegistryInterface : IRegistryInterface
    {
        private static readonly ILogger Logger = LogManager.GetLogger<RegistryInterface>();

        private readonly IMobilePartyRegistry partyRegistry;
        private readonly IHeroRegistry heroRegistry;
        private readonly IControlledHeroRegistry controlledHeroRegistry;

        public RegistryInterface(
            IMobilePartyRegistry partyRegistry,
            IHeroRegistry heroRegistry,
            IControlledHeroRegistry controlledHeroRegistry) 
        {
            this.partyRegistry = partyRegistry;
            this.heroRegistry = heroRegistry;
            this.controlledHeroRegistry = controlledHeroRegistry;
        }

        public string[] GetControlledHeroIds() => controlledHeroRegistry.ControlledHeros.ToArray();

        public void RegisterControlledHeroes(IEnumerable<string> heroIds)
        {
            if (heroIds == null)
            {
                Logger.Warning("Parameter {heroIds} was null", nameof(heroIds));
                return;
            }

            foreach (string id in heroIds)
            {
                controlledHeroRegistry.RegisterAsControlled(id);
            }
        }

        public void RegisterAllGameObjects()
        {
            partyRegistry.RegisterAllParties();
            heroRegistry.RegisterAllHeroes();
        }
    }
}
